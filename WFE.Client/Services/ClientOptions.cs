namespace WFE.Client.Services
{
    public class RabbitMqOptions
    {
        public string HostName { get; set; } = "localhost";
        public int Port { get; set; } = 5672;
        public string VirtualHost { get; set; } = "/";
        public string UserName { get; set; } = "guest";
        public string Password { get; set; } = "guest";
        public bool Ssl { get; set; } = false;

        /// <summary>Shows up as the connection name in RabbitMQ's management UI - matches
        /// your publisher's ConnectionName field.</summary>
        public string ConnectionName { get; set; } = "WFE.Client";

        /// <summary>Name of the already-existing queue to consume from - point this at your
        /// real setup.</summary>
        public string QueueName { get; set; } = "device.queue";

        /// <summary>Exchange/ExchangeType/RoutingKey mirror your publisher's queue config -
        /// only actually used if DeclareQueue=true (see below); otherwise purely
        /// documentation of what the existing queue is bound to.</summary>
        public string Exchange { get; set; } = "device.exchange";
        public string ExchangeType { get; set; } = "direct";
        public string RoutingKey { get; set; } = "inbound.tags";

        /// <summary>Matches your publisher's AutoAck setting. false (the default, and what
        /// your publisher config uses) means this client manually acks - see
        /// RabbitMqSubscriberService's class doc comment for the batch ack semantics.</summary>
        public bool AutoAck { get; set; } = false;

        /// <summary>Leave false (default) since your queue/exchange/bindings already exist and
        /// are managed elsewhere. Set true only if you want this client to declare the
        /// exchange, queue, and binding itself (using Exchange/ExchangeType/RoutingKey above)
        /// - e.g. for a from-scratch dev environment where the publisher hasn't run yet.</summary>
        public bool DeclareQueue { get; set; } = false;

        /// <summary>If true, the subscription starts automatically when the app boots - if
        /// false (the default now that Start/Stop is controllable via the dashboard/API), it
        /// stays idle until POST /api/rabbitmq/start is called.</summary>
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

    /// <summary>
    /// TEST-HARNESS CONVENIENCE ONLY - not a production pattern. Auto-invokes a Command on any
    /// Waiting instance so an evaluation run doesn't stall waiting for a human to click
    /// something. Deliberately does NOT touch instances Waiting on a Schedule trigger (those
    /// have zero available Commands - GetAvailableCommandsAsync only returns Command-triggered
    /// transitions - so they're left for WFE.Web's own ScheduleWorker, which is where that
    /// automation genuinely belongs).
    /// </summary>
    public class TestAutoAdvancerOptions
    {
        /// <summary>Disabled by default - a background process silently clicking through your
        /// commands is exactly the kind of thing that should be opt-in, not a surprise.</summary>
        public bool Enabled { get; set; } = false;

        public int PollingIntervalMs { get; set; } = 2000;

        /// <summary>Max Waiting instances inspected per poll.</summary>
        public int BatchSize { get; set; } = 50;

        /// <summary>If set, only ever invokes a command with this exact name (Title) when
        /// available on an instance - leaves it alone otherwise. If null/empty, invokes
        /// whichever command is first in that instance's available list.</summary>
        public string PreferredCommandName { get; set; }

        public string ActorId { get; set; } = "client:test-auto-advancer";
    }
}
