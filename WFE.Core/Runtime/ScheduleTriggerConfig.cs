using System;
using System.Text.Json.Serialization;

namespace WFE.Core.Runtime
{
    /// <summary>
    /// Deserialized from a &lt;Trigger Type="Schedule"&gt;'s ScheduleParameter JSON. Two modes,
    /// deliberately kept general-purpose rather than tied to any one use case:
    ///   - "Interval": re-check every IntervalSeconds (industrial polling: "check the sensor
    ///     threshold again in 30s while nothing's changed").
    ///   - "TargetDateTime": fire at/after the DateTime held in instance parameter
    ///     ParameterName (generic business process: "escalate if @Deadline has passed",
    ///     "follow up at @NextContactDate").
    /// Cron-style expressions weren't requested and add a real dependency/complexity cost - the
    /// Mode field here leaves room to add one later without a breaking change.
    /// </summary>
    public class ScheduleTriggerConfig
    {
        [JsonPropertyName("Mode")]
        public string Mode { get; set; }

        [JsonPropertyName("IntervalSeconds")]
        public int? IntervalSeconds { get; set; }

        [JsonPropertyName("ParameterName")]
        public string ParameterName { get; set; }
    }

    public static class ScheduleCalculator
    {
        /// <summary>Computes when a Schedule trigger next becomes eligible to fire, given the
        /// instance's current parameters. Throws on missing/invalid config rather than
        /// guessing - a misconfigured schedule should fail loudly at the point it's set, not
        /// silently never fire.</summary>
        public static DateTime ComputeNextFireTime(
            ScheduleTriggerConfig config,
            System.Collections.Generic.IReadOnlyDictionary<string, string> parameters,
            DateTime utcNow)
        {
            switch (config.Mode)
            {
                case "Interval":
                    if (config.IntervalSeconds is not > 0)
                        throw new InvalidOperationException(
                            "Schedule trigger Mode 'Interval' requires a positive IntervalSeconds.");
                    return utcNow.AddSeconds(config.IntervalSeconds.Value);

                case "TargetDateTime":
                    if (string.IsNullOrEmpty(config.ParameterName))
                        throw new InvalidOperationException(
                            "Schedule trigger Mode 'TargetDateTime' requires ParameterName.");
                    if (!parameters.TryGetValue(config.ParameterName, out var raw) || string.IsNullOrEmpty(raw))
                        throw new InvalidOperationException(
                            $"Schedule trigger Mode 'TargetDateTime' references parameter " +
                            $"'{config.ParameterName}', which is not set on this instance.");
                    if (!DateTime.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                            out var target))
                        throw new InvalidOperationException(
                            $"Schedule trigger parameter '{config.ParameterName}' value '{raw}' is not a valid DateTime.");
                    return target;

                default:
                    throw new NotSupportedException(
                        $"Unsupported Schedule trigger Mode '{config.Mode}' - expected 'Interval' or 'TargetDateTime'.");
            }
        }
    }
}
