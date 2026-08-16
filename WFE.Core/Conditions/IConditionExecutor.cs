using System.Threading.Tasks;
using WFE.Core.Runtime;

namespace WFE.Core.Conditions
{
    /// <summary>
    /// Implement this for every built-in <Condition Type="Action" NameRef="..."> the designer
    /// can emit (CheckParameter, CheckHTTPRequest, LoopIsNotCompletedAndBroken,
    /// CheckAllSubprocessesCompleted, ...). Note: Always / Otherwise / Expression condition
    /// types are handled directly by the engine, not through this interface.
    /// </summary>
    public interface IConditionExecutor
    {
        /// <summary>Must match the Condition NameRef value used in the schema XML exactly.</summary>
        string Name { get; }

        Task<bool> EvaluateAsync(WorkflowExecutionContext context, string rawJsonParameters);
    }
}
