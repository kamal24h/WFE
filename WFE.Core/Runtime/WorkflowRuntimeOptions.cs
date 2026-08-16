namespace WFE.Core.Runtime
{
    public class WorkflowRuntimeOptions
    {
        /// <summary>Ceiling on Auto-transition hops per Start/ExecuteCommand call - the safety
        /// net against a schema bug creating an unbounded loop that never yields. Raised from
        /// an earlier hardcoded 100 once real Loop support landed: your own LoopForDateTime.xml
        /// sample (32 iterations x ~3 hops) already used ~96 of that budget, and a ForEach loop
        /// over a larger list (or a longer date range) would blow through 100 immediately even
        /// though nothing is actually wrong. 10,000 is a generous default; raise it further via
        /// config (WorkflowRuntime:MaxAutoHops in appsettings.json) if a legitimate loop needs
        /// more, or lower it for tighter per-packet latency guarantees instead.</summary>
        public int MaxAutoHops { get; set; } = 10_000;
    }
}
