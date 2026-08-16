using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using WFE.Core.Conditions;
using WFE.Core.Runtime;
using WFE.Runtime.BuiltInActions;

namespace WFE.Runtime.BuiltInConditions
{
    public class LoopIsNotCompletedAndBrokenArgs
    {
        [JsonPropertyName("LoopName")] public string LoopName { get; set; }
    }

    /// <summary>
    /// &lt;Condition Type="Action" NameRef="LoopIsNotCompletedAndBroken"&gt; - matches both
    /// LoopForDateTime.xml and LoopForeach.xml. Only ever given LoopName (no parameter-name
    /// overrides), which is exactly why StartLoopFor/StartLoopForeach maintain their own
    /// internal LoopKeys-namespaced state independent of the schema's chosen
    /// LoopStateParameterName - this condition looks purely at that internal state.
    /// </summary>
    public class LoopIsNotCompletedAndBrokenCondition : IConditionExecutor
    {
        public string Name => "LoopIsNotCompletedAndBroken";

        public async Task<bool> EvaluateAsync(WorkflowExecutionContext context, string rawJsonParameters)
        {
            var args = JsonSerializer.Deserialize<LoopIsNotCompletedAndBrokenArgs>(rawJsonParameters,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var stateRaw = await context.Parameters.GetAsync(context.Instance.Id, LoopKeys.State(args.LoopName));
            if (stateRaw == null)
                throw new InvalidOperationException(
                    $"Loop '{args.LoopName}' has no state yet - LoopIsNotCompletedAndBroken was evaluated " +
                    "before any StartLoopFor/StartLoopForeach action ran for this LoopName. Check the schema's " +
                    "transition ordering.");

            return string.Equals(stateRaw, nameof(LoopState.InProgress), StringComparison.Ordinal);
        }
    }
}
