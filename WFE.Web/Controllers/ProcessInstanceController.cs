using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WFE.Core.Runtime;
using WFE.Persistence;
using WFE.Web.Contracts;

namespace WFE.Web.Controllers
{
    [ApiController]
    [Route("api/instances")]
    public class ProcessInstanceController : ControllerBase
    {
        private readonly IWorkflowRuntime _runtime;
        private readonly WfeDbContext _db;

        public ProcessInstanceController(IWorkflowRuntime runtime, WfeDbContext db)
        {
            _runtime = runtime;
            _db = db;
        }

        [HttpPost("start")]
        public async Task<ActionResult<WorkflowExecutionResult>> Start(
            StartInstanceRequest request, CancellationToken cancellationToken)
        {
            if (request.ProcessSchemeId == null && request.WfeSchemeId == null)
                return BadRequest(new { error = "Either ProcessSchemeId or WfeSchemeId must be provided." });

            var result = request.WfeSchemeId.HasValue
                ? await _runtime.StartInstanceFromSchemeAsync(
                    request.WfeSchemeId.Value, request.Parameters, request.ActorId, cancellationToken)
                : await _runtime.StartInstanceAsync(
                    request.ProcessSchemeId.Value, request.Parameters, request.ActorId, cancellationToken);
            return Ok(result);
        }

        /// <summary>Read-only query - goes straight to EF rather than through
        /// IProcessInstanceStore, since this is a reporting concern, not something the engine
        /// itself needs (WFE.Runtime never references WfeDbContext directly; this controller can).</summary>
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] string status = null, [FromQuery] int take = 100)
        {
            var query = _db.WfeProcessInstances.AsNoTracking().AsQueryable();
            if (!string.IsNullOrEmpty(status))
                query = query.Where(i => i.Status == status);

            var instances = await query
                .OrderByDescending(i => i.CreationDateTime)
                .Take(take)
                .ToListAsync();

            return Ok(instances);
        }

        /// <summary>Read-only query - goes straight to EF rather than through
        /// IProcessInstanceStore, since this is a reporting concern, not something the engine
        /// itself needs (WFE.Runtime never references WfeDbContext directly; this controller can).</summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(long id, [FromQuery] bool includeHistory = false, [FromQuery] bool includeParameters = false)
        {
            var instance = await _db.WfeProcessInstances.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
            if (instance == null) return NotFound();

            object history = null;
            if (includeHistory)
                history = await _db.WfeProcessTransitionsHistory.AsNoTracking()
                    .Where(h => h.ProcessInstanceId == id)
                    .OrderBy(h => h.StartTransitionTime)
                    .ToListAsync();

            object parameters = null;
            if (includeParameters)
                parameters = await _db.WfeProcessInstanceParameters.AsNoTracking()
                    .Where(p => p.ProcessInstanceId == id)
                    .ToDictionaryAsync(p => p.Name, p => p.Value);

            return Ok(new { instance, history, parameters });
        }
    }
}
