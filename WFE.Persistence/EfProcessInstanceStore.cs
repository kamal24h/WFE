using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WFE.Core.Runtime;
using WFE.Models;

namespace WFE.Persistence
{
    public class EfProcessInstanceStore : IProcessInstanceStore
    {
        private readonly WfeDbContext _db;

        public EfProcessInstanceStore(WfeDbContext db)
        {
            _db = db;
        }

        public async Task<WfeProcessInstance> CreateAsync(WfeProcessInstance instance, CancellationToken cancellationToken = default)
        {
            _db.WfeProcessInstances.Add(instance);
            await _db.SaveChangesAsync(cancellationToken);
            return instance;
        }

        public Task<WfeProcessInstance> GetAsync(long instanceId, CancellationToken cancellationToken = default) =>
            _db.WfeProcessInstances.FirstOrDefaultAsync(i => i.Id == instanceId, cancellationToken);

        public async Task SaveActivityTransitionAsync(
            WfeProcessInstance instance,
            WfeProcessTransitionHistory historyEntryOrNull,
            bool trackHistory,
            CancellationToken cancellationToken = default)
        {
            // instance is already tracked (it came from GetAsync/CreateAsync on this same
            // DbContext instance, which is scoped per-request/per-operation) - EF picks up the
            // in-place mutations WorkflowRuntime made (Activity/State/Status/etc) automatically.
            // The RowVersion property is what makes this call throw DbUpdateConcurrencyException
            // if someone else changed the row since it was loaded (e.g. a racing ExecuteCommand).
            if (trackHistory && historyEntryOrNull != null)
                _db.WfeProcessTransitionsHistory.Add(historyEntryOrNull);

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<WfeProcessInstance>> GetDueScheduledInstancesAsync(
            DateTime utcNow, int batchSize, CancellationToken cancellationToken = default)
        {
            return await _db.WfeProcessInstances
                .Where(i => i.Status == "Waiting" && i.NextScheduledCheckTime != null && i.NextScheduledCheckTime <= utcNow)
                .OrderBy(i => i.NextScheduledCheckTime)
                .Take(batchSize)
                .ToListAsync(cancellationToken);
        }

        public async Task UpdateNextScheduledCheckTimeAsync(
            long instanceId, DateTime? nextCheckTime, CancellationToken cancellationToken = default)
        {
            var instance = await _db.WfeProcessInstances.FindAsync(new object[] { instanceId }, cancellationToken);
            if (instance == null) return;
            instance.NextScheduledCheckTime = nextCheckTime;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
