using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WFE.Core.Runtime;
using WFE.Models;

namespace WFE.Persistence
{
    public class EfWorkflowParameterStore : IWorkflowParameterStore
    {
        // Perf note: Set/RemoveAsync each SaveChanges immediately, so an activity with several
        // SetParameter-style actions costs one DB round-trip per parameter. Fine for the MVP;
        // if per-packet latency becomes tight, change WorkflowExecutionContext to batch writes
        // and flush once per activity (or once per instance) instead of per-call.
        private readonly WfeDbContext _db;

        public EfWorkflowParameterStore(WfeDbContext db)
        {
            _db = db;
        }

        public async Task<string> GetAsync(long processInstanceId, string name, bool forRootProcess = false)
        {
            // TODO(Phase 3 - subprocesses): forRootProcess should resolve to the root
            // instance's id via the (not-yet-built) parent/root instance chain. Until
            // subprocesses exist, every instance IS its own root, so this is a no-op.
            var row = await _db.WfeProcessInstanceParameters
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProcessInstanceId == processInstanceId && p.Name == name);
            return row?.Value;
        }

        public async Task SetAsync(long processInstanceId, string name, string value, bool forRootProcess = false)
        {
            var row = await _db.WfeProcessInstanceParameters
                .FirstOrDefaultAsync(p => p.ProcessInstanceId == processInstanceId && p.Name == name);

            if (row == null)
            {
                _db.WfeProcessInstanceParameters.Add(new WfeProcessInstanceParameter
                {
                    ProcessInstanceId = processInstanceId,
                    Name = name,
                    Value = value,
                    ForRootProcess = forRootProcess
                });
            }
            else
            {
                row.Value = value;
            }

            await _db.SaveChangesAsync();
        }

        public async Task RemoveAsync(long processInstanceId, string name, bool forRootProcess = false)
        {
            var row = await _db.WfeProcessInstanceParameters
                .FirstOrDefaultAsync(p => p.ProcessInstanceId == processInstanceId && p.Name == name);
            if (row == null) return;

            _db.WfeProcessInstanceParameters.Remove(row);
            await _db.SaveChangesAsync();
        }

        public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(long processInstanceId)
        {
            var rows = await _db.WfeProcessInstanceParameters
                .AsNoTracking()
                .Where(p => p.ProcessInstanceId == processInstanceId)
                .ToListAsync();
            return rows.ToDictionary(p => p.Name, p => p.Value);
        }
    }
}
