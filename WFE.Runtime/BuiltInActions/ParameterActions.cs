using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using WFE.Core.Actions;
using WFE.Core.Runtime;

namespace WFE.Runtime.BuiltInActions
{
    public class SetParameterArgs
    {
        [JsonPropertyName("ParameterName")] public string ParameterName { get; set; }
        [JsonPropertyName("Value")] public string Value { get; set; }
        [JsonPropertyName("ForRootProcess")] public bool ForRootProcess { get; set; }
    }

    /// <summary>&lt;ActionRef NameRef="SetParameter"&gt; - matches ParametersAndExpressions.xml.</summary>
    public class SetParameterAction : IActionExecutor
    {
        public string Name => "SetParameter";

        public Task ExecuteAsync(WorkflowExecutionContext context, string rawJsonParameters)
        {
            var args = JsonSerializer.Deserialize<SetParameterArgs>(rawJsonParameters,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // TODO(Phase 3): honor ForRootProcess once subprocesses exist.
            return context.Parameters.SetAsync(context.Instance.Id, args.ParameterName, args.Value, args.ForRootProcess);
        }
    }

    public class RemoveParameterArgs
    {
        [JsonPropertyName("ParameterName")] public string ParameterName { get; set; }
        [JsonPropertyName("ForRootProcess")] public bool ForRootProcess { get; set; }
    }

    /// <summary>&lt;ActionRef NameRef="RemoveParameter"&gt; - matches FileWriteRead.xml.</summary>
    public class RemoveParameterAction : IActionExecutor
    {
        public string Name => "RemoveParameter";

        public Task ExecuteAsync(WorkflowExecutionContext context, string rawJsonParameters)
        {
            var args = JsonSerializer.Deserialize<RemoveParameterArgs>(rawJsonParameters,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return context.Parameters.RemoveAsync(context.Instance.Id, args.ParameterName, args.ForRootProcess);
        }
    }

    public class AddNumberToParameterArgs
    {
        [JsonPropertyName("Number")] public double Number { get; set; }
        [JsonPropertyName("ParameterName")] public string ParameterName { get; set; }
    }

    /// <summary>
    /// &lt;ActionRef NameRef="AddNumberToParameter"&gt; - matches ParametersAndExpressions.xml.
    /// Your sample implemented this via a CodeAction (dynamic C#); this is the equivalent
    /// built-in, since we deferred dynamic CodeActions to Phase 4.
    /// Deviates from the original CodeAction in one way: if the parameter doesn't exist yet,
    /// this treats it as 0 rather than throwing (a friendlier default for a counter-style use).
    /// </summary>
    public class AddNumberToParameterAction : IActionExecutor
    {
        public string Name => "AddNumberToParameter";

        public async Task ExecuteAsync(WorkflowExecutionContext context, string rawJsonParameters)
        {
            var args = JsonSerializer.Deserialize<AddNumberToParameterArgs>(rawJsonParameters,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var current = await context.Parameters.GetAsync(context.Instance.Id, args.ParameterName);
            var currentValue = 0.0;
            if (!string.IsNullOrEmpty(current))
                currentValue = double.Parse(current, NumberStyles.Float, CultureInfo.InvariantCulture);

            var updated = currentValue + args.Number;
            await context.Parameters.SetAsync(context.Instance.Id, args.ParameterName,
                updated.ToString(CultureInfo.InvariantCulture));
        }
    }
}
