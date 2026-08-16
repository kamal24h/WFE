using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WFE.Core.Runtime;
using WFE.Models;
using WFE.Web.Contracts;

namespace WFE.Web.Controllers
{
    [ApiController]
    [Route("api/instances/{instanceId}/commands")]
    public class CommandController : ControllerBase
    {
        private readonly IWorkflowRuntime _runtime;

        public CommandController(IWorkflowRuntime runtime)
        {
            _runtime = runtime;
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<WfeCommand>>> GetAvailable(
            long instanceId, [FromQuery] string actorId, CancellationToken cancellationToken)
        {
            var commands = await _runtime.GetAvailableCommandsAsync(instanceId, actorId, cancellationToken);
            return Ok(commands);
        }

        [HttpPost("{commandName}")]
        public async Task<ActionResult<WorkflowExecutionResult>> Execute(
            long instanceId, string commandName, ExecuteCommandRequest request, CancellationToken cancellationToken)
        {
            var result = await _runtime.ExecuteCommandAsync(
                instanceId, commandName, request.ActorId, request.Parameters, cancellationToken);
            return Ok(result);
        }
    }
}
