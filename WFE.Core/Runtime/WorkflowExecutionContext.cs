using System.Threading;
using System.Threading.Tasks;
using WFE.Models;

namespace WFE.Core.Runtime
{
    /// <summary>
    /// Everything a pluggable IActionExecutor / IConditionExecutor needs to do its job,
    /// without depending on EF Core or ASP.NET Core directly (keeps Core testable/host-agnostic).
    /// </summary>
    public class WorkflowExecutionContext
    {
        public WorkflowExecutionContext(
            WfeProcessInstance instance,
            IWorkflowParameterStore parameters,
            string actorId,
            CancellationToken cancellationToken = default)
        {
            Instance = instance;
            Parameters = parameters;
            ActorId = actorId;
            CancellationToken = cancellationToken;
        }

        public WfeProcessInstance Instance { get; }
        public IWorkflowParameterStore Parameters { get; }

        /// <summary>The user/system identity that caused this step to run (command invoker, or "system" for Auto).</summary>
        public string ActorId { get; }

        public CancellationToken CancellationToken { get; }
    }

    /// <summary>
    /// Read/write access to a process instance's named parameters (the "@TestParameter" values
    /// referenced by expressions, SetParameter/RemoveParameter actions, CheckParameter conditions).
    /// Backed by the WfeProcessInstanceParameter table.
    /// </summary>
    public interface IWorkflowParameterStore
    {
        Task<string> GetAsync(long processInstanceId, string name, bool forRootProcess = false);
        Task SetAsync(long processInstanceId, string name, string value, bool forRootProcess = false);
        Task RemoveAsync(long processInstanceId, string name, bool forRootProcess = false);
        Task<System.Collections.Generic.IReadOnlyDictionary<string, string>> GetAllAsync(long processInstanceId);
    }
}
