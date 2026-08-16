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
    public class EfProcessWorkItemStore : IProcessWorkItemStore, ISubprocessTracker
    {
        private readonly WfeDbContext _db;

        public EfProcessWorkItemStore(WfeDbContext db)
        {
            _db = db;
        }

        public async Task EnqueueAsync(WfeProcessWorkItem item, CancellationToken cancellationToken = default)
        {
            _db.WfeProcessWorkItems.Add(item);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<WfeProcessWorkItem>> ClaimBatchAsync(int batchSize, CancellationToken cancellationToken = default)
        {
            var candidates = await _db.WfeProcessWorkItems
                .Where(w => w.Status == "Pending")
                .OrderBy(w => w.CreatedDateTime)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            var claimed = new List<WfeProcessWorkItem>();
            foreach (var item in candidates)
            {
                item.Status = "Processing";
                item.ClaimedDateTime = DateTime.UtcNow;
                try
                {
                    await _db.SaveChangesAsync(cancellationToken);
                    claimed.Add(item);
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Another worker instance claimed it first between our read and this write -
                    // detach so it doesn't poison later SaveChanges calls in this same batch,
                    // and just skip it (it'll be picked up next poll if still Pending).
                    _db.Entry(item).State = EntityState.Detached;
                }
            }
            return claimed;
        }

        public async Task MarkCompletedAsync(long workItemId, CancellationToken cancellationToken = default)
        {
            var item = await _db.WfeProcessWorkItems.FindAsync(new object[] { workItemId }, cancellationToken);
            if (item == null) return;
            item.Status = "Completed";
            item.CompletedDateTime = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task MarkFaultedAsync(long workItemId, string error, CancellationToken cancellationToken = default)
        {
            var item = await _db.WfeProcessWorkItems.FindAsync(new object[] { workItemId }, cancellationToken);
            if (item == null) return;
            item.Status = "Faulted";
            item.Error = error;
            item.CompletedDateTime = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public Task<int> CountSpawnedAsync(long parentInstanceId, CancellationToken cancellationToken = default) =>
            _db.WfeProcessWorkItems.CountAsync(w => w.ParentInstanceId == parentInstanceId, cancellationToken);

        public Task<int> CountCompletedAsync(long parentInstanceId, CancellationToken cancellationToken = default) =>
            _db.WfeProcessInstances.CountAsync(
                i => i.ParentInstanceId == parentInstanceId && i.Status == "Completed", cancellationToken);
    }
}
