using System.Threading;
using System.Threading.Tasks;
using WFE.Models;

namespace WFE.Core.Runtime
{
    /// <summary>
    /// CRUD the runtime needs on WfeProcessInstance/WfeProcessTransitionHistory, without
    /// depending on EF Core types directly. SaveActivityTransitionAsync is the one call that
    /// matters most: it atomically moves Activity/State forward AND (if the scheme's
    /// TrackHistory is true) appends the history row, using RowVersion for optimistic
    /// concurrency so a racing ExecuteCommand can't silently clobber an in-flight auto-transition.
    /// </summary>
    public interface IProcessInstanceStore
    {
        Task<WfeProcessInstance> CreateAsync(WfeProcessInstance instance, CancellationToken cancellationToken = default);

        Task<WfeProcessInstance> GetAsync(long instanceId, CancellationToken cancellationToken = default);

        /// <summary>Persists the instance's new Activity/State/Status (and, if trackHistory,
        /// a WfeProcessTransitionHistory row). Throws a concurrency exception if instance.RowVersion
        /// is stale (i.e. someone else changed it since it was loaded).</summary>
        Task SaveActivityTransitionAsync(
            WfeProcessInstance instance,
            WfeProcessTransitionHistory historyEntryOrNull,
            bool trackHistory,
            CancellationToken cancellationToken = default);

        /// <summary>Instances currently due for a Schedule-trigger check
        /// (Status='Waiting' AND NextScheduledCheckTime &lt;= utcNow), oldest-due first.</summary>
        Task<System.Collections.Generic.IReadOnlyList<WfeProcessInstance>> GetDueScheduledInstancesAsync(
            System.DateTime utcNow, int batchSize, CancellationToken cancellationToken = default);

        /// <summary>Lightweight update used when a Schedule check fires nothing (conditions
        /// not yet satisfied) - just pushes NextScheduledCheckTime forward without going
        /// through the full transition-application path.</summary>
        Task UpdateNextScheduledCheckTimeAsync(
            long instanceId, System.DateTime? nextCheckTime, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Resolves a WfeProcessScheme id to its immutable runtime snapshot, parsed and indexed.
    /// Wraps ProcessSchemaLoader's in-memory cache with the DB fetch for cache misses.
    /// </summary>
    public interface IProcessSchemeProvider
    {
        Task<(WfeProcessScheme Scheme, Schema.ResolvedProcessSchema Resolved)> GetAsync(
            long processSchemeId, CancellationToken cancellationToken = default);
    }
}
