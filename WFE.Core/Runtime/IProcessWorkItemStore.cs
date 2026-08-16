using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WFE.Models;

namespace WFE.Core.Runtime
{
    /// <summary>
    /// The durable queue backing async ("AnotherThread") subprocess spawns. WorkflowRuntime
    /// enqueues here when a Fork/Start transition fires; WFE.Runtime.Scheduling.SubprocessWorker
    /// (a BackgroundService) claims and processes items independently, so the original caller
    /// (e.g. a packet-ingestion request) never waits on the subprocess.
    /// </summary>
    public interface IProcessWorkItemStore
    {
        Task EnqueueAsync(WfeProcessWorkItem item, CancellationToken cancellationToken = default);

        /// <summary>Atomically claims up to batchSize Pending items (Status -> Processing),
        /// safe for multiple concurrent worker instances via optimistic concurrency - a claim
        /// that loses the race is silently skipped, not retried within this call.</summary>
        Task<IReadOnlyList<WfeProcessWorkItem>> ClaimBatchAsync(int batchSize, CancellationToken cancellationToken = default);

        Task MarkCompletedAsync(long workItemId, CancellationToken cancellationToken = default);
        Task MarkFaultedAsync(long workItemId, string error, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Backs the CheckAllSubprocessesCompleted condition - "how many subprocesses did this
    /// instance ever spawn, and how many of them have completed". Spawned count is read from
    /// the work-item queue (stable the instant a fork is enqueued, even before any worker has
    /// picked it up) rather than from child WfeProcessInstance rows, which may not exist yet.
    /// </summary>
    public interface ISubprocessTracker
    {
        Task<int> CountSpawnedAsync(long parentInstanceId, CancellationToken cancellationToken = default);
        Task<int> CountCompletedAsync(long parentInstanceId, CancellationToken cancellationToken = default);
    }
}
