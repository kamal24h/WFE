using System.Threading.Tasks;
using WFE.Core.Runtime;

namespace WFE.Core.Actions
{
    /// <summary>
    /// Implement this for every built-in <ActionRef NameRef="..."> the designer can emit
    /// (SetParameter, RemoveParameter, HTTPRequest, FileWrite, FileRead, FileDelete,
    /// AddNumberToParameter, StartLoopFor, StartLoopForeach, PublishMessage, ...).
    /// Register implementations in DI keyed by <see cref="Name"/>; the engine resolves by NameRef.
    /// </summary>
    public interface IActionExecutor
    {
        /// <summary>Must match the ActionRef NameRef value used in the schema XML exactly.</summary>
        string Name { get; }

        /// <summary>
        /// Executes the action. <paramref name="rawJsonParameters"/> is the raw ActionParameter
        /// CDATA content (JSON) - deserialize it into whatever strongly-typed args class this
        /// action needs.
        /// </summary>
        Task ExecuteAsync(WorkflowExecutionContext context, string rawJsonParameters);
    }
}
