using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WFE.Models;

namespace WFE.Core.Runtime
{
    public interface IWorkflowRuntime
    {
        /// <summary>Creates a new instance at the scheme's initial activity and runs it through
        /// as many Auto transitions as it can, stopping at a final activity, a Command/Schedule
        /// wait point, or a fault.</summary>
        Task<WorkflowExecutionResult> StartInstanceAsync(
            long processSchemeId,
            IReadOnlyDictionary<string, string> initialParameters,
            string actorId,
            CancellationToken cancellationToken = default);

        /// <summary>Convenience wrapper for the sensor pipeline: seeds Tag/Value/Metadata as
        /// instance parameters and starts a new (always-fresh, per your requirement) instance.
        /// actorId should identify the calling ingestion source/service, not a placeholder.</summary>
        Task<WorkflowExecutionResult> ProcessPacketAsync(
            long processSchemeId,
            WfePacket packet,
            string actorId,
            CancellationToken cancellationToken = default);

        /// <summary>Test/evaluation fast path: given a design-time WfeScheme id (not a
        /// WfeProcessScheme id), creates a fresh immutable snapshot of its CURRENT XML and
        /// starts a new instance against it in one call - no separate publish step, and later
        /// edits to the WfeScheme can never retroactively affect this instance. Every call
        /// creates a new WfeProcessScheme row (no reuse) - fine for iterating on a workflow
        /// during testing; for a production scheme that many instances should share, use the
        /// SchemeDesignerController publish flow (WfeCommand/ProcessSchemeId-based) instead.</summary>
        Task<WorkflowExecutionResult> StartInstanceFromSchemeAsync(
            long schemeId,
            IReadOnlyDictionary<string, string> initialParameters,
            string actorId,
            CancellationToken cancellationToken = default);

        /// <summary>Same idea as ProcessPacketAsync, but keyed by WfeScheme id per
        /// StartInstanceFromSchemeAsync - the endpoint WFE.Client actually calls.</summary>
        Task<WorkflowExecutionResult> ProcessPacketFromSchemeAsync(
            long schemeId,
            WfePacket packet,
            string actorId,
            CancellationToken cancellationToken = default);

        /// <summary>Explicitly invokes a Command-triggered transition from the instance's
        /// current activity, then continues the Auto-transition loop from wherever it lands.</summary>
        Task<WorkflowExecutionResult> ExecuteCommandAsync(
            long instanceId,
            string commandName,
            string actorId,
            IReadOnlyDictionary<string, string> parameters = null,
            CancellationToken cancellationToken = default);

        /// <summary>Called by WFE.Runtime.Scheduling.ScheduleWorker once an instance's
        /// NextScheduledCheckTime has passed - evaluates that activity's Schedule-triggered
        /// transitions and fires the first satisfied one, continuing the Auto-transition loop
        /// from there. If none are satisfied yet, NextScheduledCheckTime is pushed forward and
        /// the instance stays Waiting. Not typically called directly by controllers.</summary>
        Task<WorkflowExecutionResult> ResumeScheduledAsync(
            long instanceId,
            CancellationToken cancellationToken = default);

        /// <summary>Commands currently invokable on this instance from its current activity.</summary>
        Task<IReadOnlyList<WfeCommand>> GetAvailableCommandsAsync(
            long instanceId,
            string actorId,
            CancellationToken cancellationToken = default);

        /// <summary>Starts a subprocess instance at a specific activity (not necessarily the
        /// scheme's IsInitial activity), linked to its parent/root. Called by
        /// WFE.Runtime.Scheduling.SubprocessWorker when processing a queued fork - not
        /// typically called directly by controllers.</summary>
        Task<WorkflowExecutionResult> StartChildInstanceAsync(
            long processSchemeId,
            string activityName,
            IReadOnlyDictionary<string, string> parameters,
            string actorId,
            long parentInstanceId,
            long rootInstanceId,
            string forkTransitionName,
            CancellationToken cancellationToken = default);
    }
}
