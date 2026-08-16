using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using WFE.Core.Actions;
using WFE.Core.Runtime;

namespace WFE.Runtime.BuiltInActions
{
    public class StartLoopForeachArgs
    {
        [JsonPropertyName("LoopName")] public string LoopName { get; set; }
        [JsonPropertyName("LoopStateParameterName")] public string LoopStateParameterName { get; set; }
        [JsonPropertyName("LoopCounterValueParameterName")] public string LoopCounterValueParameterName { get; set; }
        [JsonPropertyName("Separator")] public string Separator { get; set; } = ",";
        [JsonPropertyName("Values")] public string Values { get; set; }
        [JsonPropertyName("ValuesFromParameter")] public bool ValuesFromParameter { get; set; }
        [JsonPropertyName("ValuesParameterName")] public string ValuesParameterName { get; set; }
    }

    /// <summary>
    /// &lt;ActionRef NameRef="StartLoopForeach"&gt; - matches LoopForeach.xml. Same re-entrant
    /// pattern as StartLoopForAction (see its doc comment). The resolved value list is snapshotted
    /// into internal state on first entry (as JSON) so that if ValuesFromParameter reads from a
    /// parameter that later changes mid-loop, the loop still iterates over what it started with.
    /// </summary>
    public class StartLoopForeachAction : IActionExecutor
    {
        public string Name => "StartLoopForeach";

        public async Task ExecuteAsync(WorkflowExecutionContext context, string rawJsonParameters)
        {
            var args = JsonSerializer.Deserialize<StartLoopForeachArgs>(rawJsonParameters,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var instanceId = context.Instance.Id;
            var stateKey = LoopKeys.State(args.LoopName);
            var indexKey = LoopKeys.Index(args.LoopName);
            var valuesKey = LoopKeys.ValuesJson(args.LoopName);

            var alreadyStarted = await context.Parameters.GetAsync(instanceId, stateKey) != null;

            string[] values;
            int index;

            if (!alreadyStarted)
            {
                var source = args.ValuesFromParameter
                    ? await context.Parameters.GetAsync(instanceId, args.ValuesParameterName) ?? string.Empty
                    : args.Values ?? string.Empty;

                var separator = string.IsNullOrEmpty(args.Separator) ? "," : args.Separator;
                values = source.Split(new[] { separator }, StringSplitOptions.None)
                    .Select(v => v.Trim())
                    .Where(v => v.Length > 0)
                    .ToArray();

                index = 0;
                await context.Parameters.SetAsync(instanceId, valuesKey, JsonSerializer.Serialize(values));
            }
            else
            {
                var valuesJson = await context.Parameters.GetAsync(instanceId, valuesKey);
                values = JsonSerializer.Deserialize<string[]>(valuesJson);

                var prevIndexRaw = await context.Parameters.GetAsync(instanceId, indexKey);
                index = int.Parse(prevIndexRaw, CultureInfo.InvariantCulture) + 1;
            }

            var withinRange = values.Length > 0 && index < values.Length;
            var newState = withinRange ? LoopState.InProgress : LoopState.Completed;

            await context.Parameters.SetAsync(instanceId, stateKey, newState.ToString());
            await context.Parameters.SetAsync(instanceId, args.LoopStateParameterName, newState.ToString());

            if (withinRange)
            {
                await context.Parameters.SetAsync(instanceId, indexKey, index.ToString(CultureInfo.InvariantCulture));
                await context.Parameters.SetAsync(instanceId, args.LoopCounterValueParameterName, values[index]);
            }
        }
    }
}
