namespace WFE.Client.Services
{
    public class RabbitMqOptions
    {
        public string HostName { get; set; } = "localhost";
        public int Port { get; set; } = 5672;
        public string VirtualHost { get; set; } = "/";
        public string UserName { get; set; } = "guest";
        public string Password { get; set; } = "guest";

        /// <summary>Name of the already-existing queue to consume from - point this at your
        /// real setup. This client does not create exchanges or bindings; it only consumes.</summary>
        public string QueueName { get; set; } = "wfe.packets";

        /// <summary>Leave false (default) since your queue/exchange/bindings already exist and
        /// are managed elsewhere. Set true only if you want this client to declare the queue
        /// itself (durable, non-exclusive, non-auto-delete) for a from-scratch environment.</summary>
        public bool DeclareQueue { get; set; } = false;

        /// <summary>False by default so the app runs and the manual test form works
        /// immediately, with no broker connection required. Flip to true once the connection
        /// settings above point at your real RabbitMQ.</summary>
        public bool AutoConnect { get; set; } = false;

        /// <summary>Each message now carries a batch of up to 500 sensor records - this bounds
        /// how many of that batch's records are in flight to the engine at once, so one huge
        /// batch doesn't fire 500 simultaneous HTTP calls.</summary>
        public int MaxConcurrentIngests { get; set; } = 10;
    }

    public class WfeApiOptions
    {
        /// <summary>Base URL of the WFE.Web engine API this client talks to.</summary>
        public string BaseUrl { get; set; } = "https://localhost:51113/";

        /// <summary>The design-time WfeScheme to test against - every packet starts a new
        /// instance via IWorkflowRuntime.ProcessPacketFromSchemeAsync, which snapshots this
        /// scheme's CURRENT XML into a fresh WfeProcessScheme at that exact moment and runs
        /// against that snapshot. Editing the WfeScheme later never affects an instance already
        /// started - each run gets its own immutable copy.</summary>
        public long WfeSchemeId { get; set; }

        /// <summary>Identifies this client as the calling actor in the engine's audit trail -
        /// stand-in for your real ingestion microservice's own identity.</summary>
        public string ActorId { get; set; } = "client:rabbitmq-subscriber";
    }
}
