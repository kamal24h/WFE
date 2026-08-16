using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using WFE.Core.Runtime;

namespace WFE.Core.Actions
{
    /// <summary>
    /// Deliberately minimal and transport-agnostic - implement this against RabbitMQ, Kafka,
    /// MQTT, Azure Service Bus, whatever your plant already uses. The engine only needs to
    /// hand off a topic/routing key and a payload; it doesn't care how it gets there.
    /// </summary>
    public interface IMessageBroker
    {
        Task PublishAsync(string topic, string payload, System.Threading.CancellationToken cancellationToken = default);
    }

    public class PublishMessageParameters
    {
        [JsonPropertyName("topic")]
        public string Topic { get; set; }

        /// <summary>
        /// If true, publishes every current instance parameter (e.g. Tag/Value) as a JSON
        /// object. If false, publishes only the parameters named in Fields.
        /// </summary>
        [JsonPropertyName("publishAllParameters")]
        public bool PublishAllParameters { get; set; } = true;

        [JsonPropertyName("fields")]
        public string[] Fields { get; set; }
    }

    /// <summary>
    /// &lt;ActionRef NameRef="PublishMessage"&gt;
    ///   &lt;ActionParameter&gt;{"Topic":"plant/line1/processed","PublishAllParameters":true}&lt;/ActionParameter&gt;
    /// &lt;/ActionRef&gt;
    /// This is the hand-off point to your downstream processing - the engine's job ends at
    /// "decide + publish", heavy processing/persistence happens in the subscriber.
    /// </summary>
    public class PublishMessageAction : IActionExecutor
    {
        private readonly IMessageBroker _broker;

        public PublishMessageAction(IMessageBroker broker)
        {
            _broker = broker;
        }

        public string Name => "PublishMessage";

        public async Task ExecuteAsync(WorkflowExecutionContext context, string rawJsonParameters)
        {
            var args = JsonSerializer.Deserialize<PublishMessageParameters>(rawJsonParameters,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var allParams = await context.Parameters.GetAllAsync(context.Instance.Id);

            System.Collections.Generic.IReadOnlyDictionary<string, string> payloadSource = args.PublishAllParameters
                ? allParams
                : allParams.Where(kv => args.Fields != null && System.Array.IndexOf(args.Fields, kv.Key) >= 0)
                    .ToDictionary(kv => kv.Key, kv => kv.Value);

            var payload = JsonSerializer.Serialize(payloadSource);
            await _broker.PublishAsync(args.Topic, payload, context.CancellationToken);
        }
    }
}
