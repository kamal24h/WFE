namespace WFE.Core.Runtime
{
    public class SubprocessWorkerOptions
    {
        /// <summary>How often the worker polls for Pending work items. This is the real
        /// latency cost of "AnotherThread" - a fork enqueued right after a poll can wait up to
        /// this long before the subprocess actually starts. Tune down for latency-sensitive
        /// pipelines, up to reduce idle polling load.</summary>
        public int PollingIntervalMs { get; set; } = 500;

        /// <summary>Max work items claimed per poll.</summary>
        public int BatchSize { get; set; } = 20;
    }
}
