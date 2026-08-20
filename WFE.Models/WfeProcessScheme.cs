namespace WFE.Models
{
    // Resolved, IMMUTABLE runtime snapshot of a scheme (subprocesses inlined, activities/
    // starting transition resolved). Instances execute against THIS, not against the mutable
    // design-time WfeScheme row - so republishing a design never changes the behavior of
    // already-running instances.
    public class WfeProcessScheme
    {
        public long Id { get; set; }

        // Traceability back to the design-time row this snapshot was published from.
        // Nullable because inlined subprocess schemes may not map 1:1 to a single WfeScheme.
        public long? SchemeId { get; set; }

        public string Scheme { get; set; }
        public string DefiningParameters { get; set; }
        public bool IsObsolete { get; set; }
        public string RootSchemeCode { get; set; }
        public long? RootSchemeId { get; set; }
        public string AllowedActivities { get; set; }
        public string StartingTransition { get; set; }

        // High-throughput packet pipelines: when false, the engine does NOT write a
        // WfeProcessTransitionHistory row for every hop, and does not persist the
        // WfeProcessInstance itself unless it ends in an error or a designated "durable"
        // final activity. Default true for normal human-facing workflows.
        public bool TrackHistory { get; set; } = true;

        public virtual WfeScheme SourceScheme { get; set; }
    }
}
