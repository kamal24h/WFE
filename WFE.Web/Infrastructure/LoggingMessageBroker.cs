using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WFE.Core.Actions;

namespace WFE.Web.Infrastructure
{
    /// <summary>
    /// TEMPORARY placeholder so PublishMessage has something to resolve in DI. Replace with a
    /// real client for whatever broker your plant uses (RabbitMQ/Kafka/MQTT/Azure Service Bus) -
    /// this implementation does NOT deliver messages anywhere, it only logs what would have
    /// been published. Do not ship this to production.
    /// </summary>
    public class LoggingMessageBroker : IMessageBroker
    {
        private readonly ILogger<LoggingMessageBroker> _logger;

        public LoggingMessageBroker(ILogger<LoggingMessageBroker> logger)
        {
            _logger = logger;
        }

        public Task PublishAsync(string topic, string payload, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("[STUB BROKER] would publish to {Topic}: {Payload}", topic, payload);
            return Task.CompletedTask;
        }
    }
}
