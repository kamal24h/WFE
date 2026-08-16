using System;
using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WFE.Core.Actions;
using WFE.Core.Runtime;

namespace WFE.Runtime.BuiltInActions
{
    public class StartLoopForArgs
    {
        [JsonPropertyName("LoopName")] public string LoopName { get; set; }
        [JsonPropertyName("LoopStateParameterName")] public string LoopStateParameterName { get; set; }
        [JsonPropertyName("LoopCounterValueParameterName")] public string LoopCounterValueParameterName { get; set; }

        /// <summary>"DateTime" confirmed from your sample. "Number" is MY assumption for the
        /// numeric counterpart (not shown in any sample) - confirm the actual literal your
        /// designer emits for a numeric StartLoopFor and I'll adjust the switch below.</summary>
        [JsonPropertyName("CounterType")] public string CounterType { get; set; }

        /// <summary>"Increment" confirmed from your sample; "Decrement" supported symmetrically.</summary>
        [JsonPropertyName("StepType")] public string StepType { get; set; } = "Increment";

        [JsonPropertyName("StartValue")] public string StartValue { get; set; }
        [JsonPropertyName("EndValue")] public string EndValue { get; set; }

        /// <summary>DateTime steps use "&lt;number&gt;&lt;d|h|m|s&gt;" (e.g. "1d", "30m") per
        /// your sample. Number steps are just a plain numeric string (e.g. "1", "0.5").</summary>
        [JsonPropertyName("Step")] public string Step { get; set; }

        [JsonPropertyName("IncludeLastValue")] public bool IncludeLastValue { get; set; } = true;
    }

    /// <summary>
    /// &lt;ActionRef NameRef="StartLoopFor"&gt; - matches LoopForDateTime.xml. Re-entrant by
    /// design: the schema wires this activity's own loop-back transition to point right back
    /// at it, so this action runs once per iteration - first call initializes at StartValue,
    /// every subsequent call (detected via whether loop state already exists) advances by Step.
    /// Whether the loop keeps going is entirely encoded in LoopState (see LoopKeys) - the
    /// paired LoopIsNotCompletedAndBroken condition on the outbound transitions is what
    /// actually decides whether to re-enter the loop body or exit to Otherwise.
    /// </summary>
    public class StartLoopForAction : IActionExecutor
    {
        public string Name => "StartLoopFor";

        public async Task ExecuteAsync(WorkflowExecutionContext context, string rawJsonParameters)
        {
            var args = System.Text.Json.JsonSerializer.Deserialize<StartLoopForArgs>(rawJsonParameters,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var instanceId = context.Instance.Id;
            var stateKey = LoopKeys.State(args.LoopName);
            var valueKey = LoopKeys.CurrentValue(args.LoopName);

            var alreadyStarted = await context.Parameters.GetAsync(instanceId, stateKey) != null;

            bool withinRange;
            string exposedValue;

            if (string.Equals(args.CounterType, "DateTime", StringComparison.OrdinalIgnoreCase))
            {
                var start = DateTime.Parse(args.StartValue, CultureInfo.InvariantCulture);
                var end = DateTime.Parse(args.EndValue, CultureInfo.InvariantCulture);
                var step = ParseDateTimeStep(args.Step);
                if (string.Equals(args.StepType, "Decrement", StringComparison.OrdinalIgnoreCase))
                    step = step.Negate();
                if (step == TimeSpan.Zero)
                    throw new InvalidOperationException(
                        $"Loop '{args.LoopName}': Step resolves to zero - this would never terminate " +
                        "(the engine's max-hop guard would eventually fault the instance, but fix the schema instead).");

                DateTime current;
                if (!alreadyStarted)
                {
                    current = start;
                }
                else
                {
                    var prevRaw = await context.Parameters.GetAsync(instanceId, valueKey);
                    var prev = DateTime.Parse(prevRaw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                    current = prev + step;
                }

                withinRange = step > TimeSpan.Zero
                    ? (args.IncludeLastValue ? current <= end : current < end)
                    : (args.IncludeLastValue ? current >= end : current > end);

                exposedValue = current.ToString("o", CultureInfo.InvariantCulture);
                if (withinRange)
                    await context.Parameters.SetAsync(instanceId, valueKey, exposedValue);
            }
            else if (string.Equals(args.CounterType, "Number", StringComparison.OrdinalIgnoreCase))
            {
                var start = double.Parse(args.StartValue, NumberStyles.Float, CultureInfo.InvariantCulture);
                var end = double.Parse(args.EndValue, NumberStyles.Float, CultureInfo.InvariantCulture);
                var step = double.Parse(args.Step, NumberStyles.Float, CultureInfo.InvariantCulture);
                if (string.Equals(args.StepType, "Decrement", StringComparison.OrdinalIgnoreCase))
                    step = -step;
                if (step == 0)
                    throw new InvalidOperationException(
                        $"Loop '{args.LoopName}': Step resolves to zero - this would never terminate.");

                double current;
                if (!alreadyStarted)
                {
                    current = start;
                }
                else
                {
                    var prevRaw = await context.Parameters.GetAsync(instanceId, valueKey);
                    current = double.Parse(prevRaw, NumberStyles.Float, CultureInfo.InvariantCulture) + step;
                }

                withinRange = step > 0
                    ? (args.IncludeLastValue ? current <= end : current < end)
                    : (args.IncludeLastValue ? current >= end : current > end);

                exposedValue = current.ToString(CultureInfo.InvariantCulture);
                if (withinRange)
                    await context.Parameters.SetAsync(instanceId, valueKey, exposedValue);
            }
            else
            {
                throw new NotSupportedException(
                    $"Unsupported CounterType '{args.CounterType}' for StartLoopFor - expected 'DateTime' or 'Number'.");
            }

            var newState = withinRange ? LoopState.InProgress : LoopState.Completed;
            await context.Parameters.SetAsync(instanceId, stateKey, newState.ToString());
            await context.Parameters.SetAsync(instanceId, args.LoopStateParameterName, newState.ToString());
            if (withinRange)
                await context.Parameters.SetAsync(instanceId, args.LoopCounterValueParameterName, exposedValue);
        }

        private static TimeSpan ParseDateTimeStep(string step)
        {
            var match = Regex.Match(step ?? string.Empty, @"^(?<num>\d+(\.\d+)?)(?<unit>[dhms])$",
                RegexOptions.IgnoreCase);
            if (!match.Success)
                throw new FormatException(
                    $"Step '{step}' is not in the supported form <number><d|h|m|s>, e.g. '1d', '30m', '45s'.");

            var num = double.Parse(match.Groups["num"].Value, CultureInfo.InvariantCulture);
            return match.Groups["unit"].Value.ToLowerInvariant() switch
            {
                "d" => TimeSpan.FromDays(num),
                "h" => TimeSpan.FromHours(num),
                "m" => TimeSpan.FromMinutes(num),
                "s" => TimeSpan.FromSeconds(num),
                _ => throw new FormatException($"Unrecognized step unit in '{step}'.")
            };
        }
    }
}
