using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WFE.Core.Runtime;
using WFE.Web.Contracts;

namespace WFE.Web.Controllers
{
    [ApiController]
    [Route("api/ingestion")]
    public class PacketIngestionController : ControllerBase
    {
        private readonly IWorkflowRuntime _runtime;

        public PacketIngestionController(IWorkflowRuntime runtime)
        {
            _runtime = runtime;
        }

        /// <summary>One packet in, one WorkflowExecutionResult out - Status tells your
        /// ingestion service whether it Completed, is Waiting on a command, or Faulted
        /// (with FaultReason). This endpoint deliberately does no processing itself; it's
        /// purely the hand-off into the engine.</summary>
        [HttpPost("packets")]
        public async Task<ActionResult<WorkflowExecutionResult>> IngestPacket(
            IngestPacketRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.ActorId))
                return BadRequest(new { error = "ActorId is required - identify the calling ingestion source." });

            if (request.ProcessSchemeId == null && request.WfeSchemeId == null)
                return BadRequest(new { error = "Either ProcessSchemeId or WfeSchemeId must be provided." });

            var packet = new WfePacket
            {
                Tag = request.Tag,
                Value = request.Value,
                Timestamp = request.Timestamp ?? System.DateTime.UtcNow,
                Metadata = request.Metadata
            };

            var result = request.WfeSchemeId.HasValue
                ? await _runtime.ProcessPacketFromSchemeAsync(request.WfeSchemeId.Value, packet, request.ActorId, cancellationToken)
                : await _runtime.ProcessPacketAsync(request.ProcessSchemeId.Value, packet, request.ActorId, cancellationToken);

            return Ok(result);
        }
    }
}
