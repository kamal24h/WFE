using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace WFE.Client.Services
{
    /// <summary>
    /// One row of a batch message: DeviceId, Tag (sensor id), Value, and whatever other fields
    /// your producer includes. Exact extra field names weren't specified, so ExtraFields
    /// captures anything beyond DeviceId/Tag/Value/Timestamp generically via JsonExtensionData
    /// rather than guessing specific property names - each one just flows through as packet
    /// Metadata. Adjust the named properties below if your actual field names differ (e.g. if
    /// "DeviceId" is really "AssetId" in your producer).
    /// </summary>
    public class SensorRecordDto
    {
        [JsonPropertyName("DeviceId")] public string DeviceId { get; set; }
        [JsonPropertyName("Tag")] public string Tag { get; set; }
        [JsonPropertyName("Value")] public string Value { get; set; }
        [JsonPropertyName("Timestamp")] public string Timestamp { get; set; }

        [JsonExtensionData] public Dictionary<string, JsonElement> ExtraFields { get; set; }
    }

    /// <summary>
    /// Consumes from your already-provisioned RabbitMQ queue. Each message is a BATCH - a JSON
    /// array of up to ~500 SensorRecordDto rows - not a single key/value packet. Every record
    /// in the batch starts its own instance (still "one lightweight instance per reading", per
    /// your original design), processed with bounded concurrency (RabbitMq:MaxConcurrentIngests)
    /// rather than 500 simultaneous HTTP calls.
    ///
    /// ACK SEMANTICS - read before relying on this for anything but evaluation: the whole
    /// message is Ack'd once every record has been ATTEMPTED, regardless of whether individual
    /// records succeeded or failed (failures are logged and visible in the dashboard, not
    /// silently dropped). This is deliberate: Nack-and-requeue for a 500-row batch because ONE
    /// record failed would redeliver all 500, including the ones that already succeeded -
    /// creating duplicate instances for those. Losing a few genuinely-failed records is the
    /// lesser evil here for a batch this size. If you need stronger guarantees, add per-record
    /// dead-lettering or an idempotency key on your side before production use.
    /// </summary>
    public class RabbitMqSubscriberService : BackgroundService
    {
        private readonly RabbitMqOptions _options;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly PacketActivityLog _log;
        private readonly ILogger<RabbitMqSubscriberService> _logger;

        private IConnection _connection;
        private IModel _channel;

        public RabbitMqSubscriberService(
            RabbitMqOptions options, IServiceScopeFactory scopeFactory, PacketActivityLog log,
            ILogger<RabbitMqSubscriberService> logger)
        {
            _options = options;
            _scopeFactory = scopeFactory;
            _log = log;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.AutoConnect)
            {
                _logger.LogInformation(
                    "RabbitMQ auto-connect is disabled (RabbitMq:AutoConnect=false) - subscriber not started. " +
                    "The manual test form on the dashboard still works without a broker.");
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    Connect();
                    _logger.LogInformation(
                        "RabbitMQ connected to {Host}:{Port}{VHost}, consuming queue '{Queue}'",
                        _options.HostName, _options.Port, _options.VirtualHost, _options.QueueName);

                    var shutdown = new TaskCompletionSource();
                    _connection.ConnectionShutdown += (_, _) => shutdown.TrySetResult();
                    await using (stoppingToken.Register(() => shutdown.TrySetResult()))
                    {
                        await shutdown.Task;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "RabbitMQ connection failed - retrying in 5s");
                }
                finally
                {
                    CleanUp();
                }

                if (!stoppingToken.IsCancellationRequested)
                {
                    try { await Task.Delay(5000, stoppingToken); }
                    catch (OperationCanceledException) { /* normal on shutdown */ }
                }
            }

            CleanUp();
        }

        private void Connect()
        {
            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                VirtualHost = _options.VirtualHost,
                UserName = _options.UserName,
                Password = _options.Password,
                DispatchConsumersAsync = true
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            if (_options.DeclareQueue)
                _channel.QueueDeclare(_options.QueueName, durable: true, exclusive: false, autoDelete: false);

            // A whole 500-row batch can take a moment to process (bounded-concurrency HTTP
            // calls) - only pull one message at a time so we're not holding several huge
            // batches unacknowledged simultaneously.
            _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += async (_, ea) =>
            {
                var payload = Encoding.UTF8.GetString(ea.Body.ToArray());
                await HandleBatchAsync(ea.RoutingKey, payload, CancellationToken.None);
                // Always Ack after attempting the batch - see class doc comment.
                _channel.BasicAck(ea.DeliveryTag, multiple: false);
            };

            _channel.BasicConsume(_options.QueueName, autoAck: false, consumer);
        }

        private void CleanUp()
        {
            try { _channel?.Close(); } catch { /* best effort */ }
            try { _connection?.Close(); } catch { /* best effort */ }
            _channel = null;
            _connection = null;
        }

        private async Task HandleBatchAsync(string routingKey, string payload, CancellationToken cancellationToken)
        {
            List<SensorRecordDto> records;
            try
            {
                records = JsonSerializer.Deserialize<List<SensorRecordDto>>(payload,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Message on routing key '{RoutingKey}' is not a valid JSON array of records - skipping", routingKey);
                return;
            }

            if (records == null || records.Count == 0)
                return;

            var succeeded = 0;
            var failed = 0;
            var gate = new SemaphoreSlim(Math.Max(1, _options.MaxConcurrentIngests));

            var tasks = records.Select(async record =>
            {
                await gate.WaitAsync(cancellationToken);
                try
                {
                    var ok = await IngestOneRecordAsync(routingKey, record, cancellationToken);
                    if (ok) Interlocked.Increment(ref succeeded); else Interlocked.Increment(ref failed);
                }
                finally
                {
                    gate.Release();
                }
            });

            await Task.WhenAll(tasks);

            _logger.LogInformation(
                "Batch on routing key '{RoutingKey}': {Count} record(s), {Succeeded} succeeded, {Failed} failed",
                routingKey, records.Count, succeeded, failed);
        }

        private async Task<bool> IngestOneRecordAsync(string routingKey, SensorRecordDto record, CancellationToken cancellationToken)
        {
            var metadata = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(record.DeviceId)) metadata["DeviceId"] = record.DeviceId;
            if (!string.IsNullOrEmpty(record.Timestamp)) metadata["Timestamp"] = record.Timestamp;
            if (record.ExtraFields != null)
                foreach (var kvp in record.ExtraFields)
                    metadata[kvp.Key] = kvp.Value.ValueKind == JsonValueKind.String
                        ? kvp.Value.GetString()
                        : kvp.Value.GetRawText();

            using var scope = _scopeFactory.CreateScope();
            var apiClient = scope.ServiceProvider.GetRequiredService<IWfeApiClient>();
            var result = await apiClient.IngestPacketAsync(record.Tag, record.Value, metadata, cancellationToken);

            _log.Add(new PacketLogEntry
            {
                Source = "RabbitMQ",
                Topic = routingKey,
                DeviceId = record.DeviceId,
                Tag = record.Tag,
                Value = record.Value,
                Result = result
            });

            if (!result.Success)
                _logger.LogWarning(
                    "Record (Device={DeviceId}, Tag={Tag}) on routing key '{RoutingKey}' failed to ingest: {Error}",
                    record.DeviceId, record.Tag, routingKey, result.ErrorMessage);

            return result.Success;
        }
    }
}
