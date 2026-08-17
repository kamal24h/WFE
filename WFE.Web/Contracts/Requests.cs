using System.Collections.Generic;

namespace WFE.Web.Contracts
{
    public class StartInstanceRequest
    {
        /// <summary>Provide exactly one of ProcessSchemeId or WfeSchemeId. ProcessSchemeId
        /// starts against an already-published, shared runtime snapshot. WfeSchemeId is the
        /// test/evaluation fast path - creates a fresh snapshot of that WfeScheme's current
        /// XML and starts against it in one call, no separate publish step.</summary>
        public long? ProcessSchemeId { get; set; }
        public long? WfeSchemeId { get; set; }

        public string ActorId { get; set; }
        public Dictionary<string, string> Parameters { get; set; }
    }

    public class IngestPacketRequest
    {
        /// <summary>Provide exactly one of ProcessSchemeId or WfeSchemeId - see
        /// StartInstanceRequest for the distinction.</summary>
        public long? ProcessSchemeId { get; set; }
        public long? WfeSchemeId { get; set; }

        /// <summary>Identifies the calling ingestion source/service (e.g. your OPC-UA/MQTT
        /// microservice's own id) - required, not defaulted, so the audit trail is meaningful.</summary>
        public string ActorId { get; set; }

        public string Tag { get; set; }
        public string Value { get; set; }
        public System.DateTime? Timestamp { get; set; }
        public Dictionary<string, string> Metadata { get; set; }
    }

    public class ExecuteCommandRequest
    {
        public string ActorId { get; set; }
        public Dictionary<string, string> Parameters { get; set; }
    }

    public class SaveSchemeRequest
    {
        public long BusinessProcessId { get; set; }
        public string Name { get; set; }
        public string SchemeXml { get; set; }
        public string Tags { get; set; }
    }

    public class PublishSchemeRequest
    {
        /// <summary>When true, marks all other WfeProcessScheme rows for the same
        /// WfeScheme/business process obsolete - the usual "this is now the live version"
        /// publish semantics. When false, publishes a new runtime snapshot alongside existing
        /// ones (e.g. for side-by-side testing).</summary>
        public bool SupersedePrevious { get; set; } = true;

        public bool TrackHistory { get; set; } = true;
    }
}
