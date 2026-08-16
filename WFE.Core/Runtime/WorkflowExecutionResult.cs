namespace WFE.Core.Runtime
{
    public enum WorkflowInstanceStatus
    {
        Running,
        Waiting,
        Completed,
        Faulted
    }

    /// <summary>
    /// What happened as a result of starting an instance, executing a command, or a scheduled
    /// tick. Callers (the ingestion controller, a command endpoint, the schedule worker) branch
    /// on Status rather than reaching into the instance/schema internals themselves.
    /// </summary>
    public class WorkflowExecutionResult
    {
        public long InstanceId { get; set; }
        public WorkflowInstanceStatus Status { get; set; }
        public string Activity { get; set; }
        public string State { get; set; }

        /// <summary>Only meaningful when Status == Faulted.</summary>
        public string FaultReason { get; set; }

        /// <summary>Optional informational text for non-fault outcomes (e.g. a command's
        /// conditions weren't satisfied - the instance is still Waiting, this just explains why
        /// nothing happened).</summary>
        public string Message { get; set; }

        /// <summary>Only meaningful when Status == Waiting - what the caller can do next.</summary>
        public System.Collections.Generic.IReadOnlyList<string> AvailableCommandNames { get; set; }
            = System.Array.Empty<string>();

        public static WorkflowExecutionResult NotFound(long instanceId) => new WorkflowExecutionResult
        {
            InstanceId = instanceId,
            Status = WorkflowInstanceStatus.Faulted,
            FaultReason = "Instance not found."
        };
    }
}
