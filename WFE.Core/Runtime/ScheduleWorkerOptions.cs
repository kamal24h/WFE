namespace WFE.Core.Runtime
{
    public class ScheduleWorkerOptions
    {
        /// <summary>How often ScheduleWorker polls for instances whose NextScheduledCheckTime
        /// has passed. This is the real latency floor for Schedule triggers - an Interval of
        /// 5s configured on a transition still won't fire meaningfully sooner than this.</summary>
        public int PollingIntervalMs { get; set; } = 1000;

        public int BatchSize { get; set; } = 50;

        /// <summary>If a due instance's Schedule transition conditions aren't satisfied yet
        /// (e.g. an Expression gate alongside the schedule), how long to wait before checking
        /// that instance again - avoids re-evaluating it on every single poll indefinitely.</summary>
        public int RetryIntervalSecondsIfNotFired { get; set; } = 30;
    }
}
