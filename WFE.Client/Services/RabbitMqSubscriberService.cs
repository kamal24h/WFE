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
    /// Metadata. Adjust the named properties below if your actual field names differ.
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
    /// Consumes from your RabbitMQ queue. Unlike a plain BackgroundService, this is now
    /// explicitly controllable at runtime: registered as BOTH a singleton (so
    /// RabbitMqController can inject it and call StartSubscribingAsync/StopSubscribingAsync on
    /// demand) and an IHostedService (so it still integrates with app startup/shutdown - see
    /// Program.cs for the "same instance, two roles" registration). RabbitMq:AutoConnect only
    /// controls whether it starts itself automatically on boot; the real control surface is
    /// Start()/Stop(), exposed via POST /api/rabbitmq/start and /stop.
    ///
    /// Each message is a BATCH - a JSON array of up to ~500 SensorRecordDto rows - not a single
    /// key/value packet. Every record starts its own instance, processed with bounded
    /// concurrency (RabbitMq:MaxConcurrentIngests).
    ///
    /// ACK SEMANTICS: if RabbitMq:AutoAck is false (matches your publisher's setting), the
    /// whole message is Ack'd once every record has been ATTEMPTED, regardless of individual
    /// success/failure (see HandleBatchAsync) - Nacking a 500-row batch over one bad record
    /// would redeliver (and duplicate-instance) the ones that already succeeded. If AutoAck is
    /// true, RabbitMQ has already acked on delivery and this client does nothing further.
    /// </summary>
    public class RabbitMqSubscriberService : IHostedService, IDisposable
    {
        private readonly RabbitMqOptions _options;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly PacketActivityLog _log;
        private readonly ILogger<RabbitMqSubscriberService> _logger;

        private readonly object _lock = new();
        private CancellationTokenSource _cts;
        private Task _runLoopTask;
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

        public bool IsRunning { get; private set; }

        // --- IHostedService: app startup/shutdown integration ---

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (_options.AutoConnect)
            {
                _logger.LogInformation("RabbitMq:AutoConnect=true - starting subscriber automatically.");
                return StartSubscribingAsync();
            }

            _logger.LogInformation(
                "RabbitMq:AutoConnect=false - subscriber idle until started (POST /api/rabbitmq/start " +
                "or the dashboard button).");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => StopSubscribingAsync();

        // --- Manual control surface, used by RabbitMqController ---

        public Task StartSubscribingAsync()
        {
            lock (_lock)
            {
                if (IsRunning)
                    return Task.CompletedTask;

                _cts = new CancellationTokenSource();
                IsRunning = true;
                _runLoopTask = Task.Run(() => RunLoopAsync(_cts.Token));
            }
            return Task.CompletedTask;
        }

        public async Task StopSubscribingAsync()
        {
            CancellationTokenSource ctsToCancel;
            Task loopTask;
            lock (_lock)
            {
                if (!IsRunning)
                    return;
                ctsToCancel = _cts;
                loopTask = _runLoopTask;
                IsRunning = false;
            }

            ctsToCancel?.Cancel();
            if (loopTask != null)
            {
                try { await loopTask; }
                catch { /* the loop handles/logs its own exceptions */ }
            }
            CleanUpConnection();
        }

        // --- Connection loop ---

        private async Task RunLoopAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    Connect();
                    _logger.LogInformation(
                        "RabbitMQ connected to {Host}:{Port}{VHost} ('{ConnectionName}'), consuming queue '{Queue}'",
                        _options.HostName, _options.Port, _options.VirtualHost, _options.ConnectionName, _options.QueueName);

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
                    CleanUpConnection();
                }

                if (!stoppingToken.IsCancellationRequested)
                {
                    try { await Task.Delay(5000, stoppingToken); }
                    catch (OperationCanceledException) { /* normal on stop */ }
                }
            }

            CleanUpConnection();
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
                ClientProvidedName = _options.ConnectionName,
                Ssl = new SslOption { Enabled = _options.Ssl, ServerName = _options.HostName },
                DispatchConsumersAsync = true
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            if (_options.DeclareQueue)
            {
                _channel.ExchangeDeclare(_options.Exchange, _options.ExchangeType, durable: true);
                _channel.QueueDeclare(_options.QueueName, durable: true, exclusive: false, autoDelete: false);
                _channel.QueueBind(_options.QueueName, _options.Exchange, _options.RoutingKey);
            }

            // A whole 500-row batch can take a moment to process (bounded-concurrency HTTP
            // calls) - only pull one message at a time so we're not holding several huge
            // batches unacknowledged simultaneously.
            _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += async (_, ea) =>
            {
                var payload = Encoding.UTF8.GetString(ea.Body.ToArray());
                await HandleBatchAsync(ea.RoutingKey, payload, CancellationToken.None);

                if (!_options.AutoAck)
                    _channel.BasicAck(ea.DeliveryTag, multiple: false);
            };

            _channel.BasicConsume(_options.QueueName, autoAck: _options.AutoAck, consumer);
        }

        private void CleanUpConnection()
        {
            try { _channel?.Close(); } catch { /* best effort */ }
            try { _connection?.Close(); } catch { /* best effort */ }
            _channel = null;
            _connection = null;
        }

        // --- Batch handling ---

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

        public void Dispose()
        {
            _cts?.Dispose();
        }
    }
}
