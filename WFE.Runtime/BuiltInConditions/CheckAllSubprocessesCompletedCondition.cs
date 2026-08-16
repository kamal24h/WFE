using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using WFE.Core.Conditions;
using WFE.Core.Runtime;

namespace WFE.Runtime.BuiltInConditions
{
    public class CheckAllSubprocessesCompletedArgs
    {
        [JsonPropertyName("Mode")] public string Mode { get; set; }
    }

    /// <summary>
    /// &lt;Condition Type="Action" NameRef="CheckAllSubprocessesCompleted"&gt; - matches
    /// ParallelProcessesWithWaiting.xml. Only "AllSubprocessesAndParent" is implemented (the
    /// only value your sample shows) - the "AndParent" half needs no extra check here, since
    /// this condition only ever runs because the parent instance itself already reached this
    /// join point; "AllSubprocesses" really means every fork this instance ever enqueued
    /// (spawned count, from the work-item queue) has a matching completed child instance.
    /// KNOWN LIMITATION: counts every fork ever spawned by this instance regardless of which
    /// transition spawned it - if a schema forks from more than one distinct fork point on the
    /// same instance (not shown in either sample), this join waits for ALL of them together,
    /// not per fork-point. Flag me if you need per-transition join scoping.
    /// </summary>
    public class CheckAllSubprocessesCompletedCondition : IConditionExecutor
    {
        private readonly ISubprocessTracker _tracker;

        public CheckAllSubprocessesCompletedCondition(ISubprocessTracker tracker)
        {
            _tracker = tracker;
        }

        public string Name => "CheckAllSubprocessesCompleted";

        public async Task<bool> EvaluateAsync(WorkflowExecutionContext context, string rawJsonParameters)
        {
            var args = JsonSerializer.Deserialize<CheckAllSubprocessesCompletedArgs>(rawJsonParameters,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (!string.Equals(args.Mode, "AllSubprocessesAndParent", StringComparison.Ordinal))
                throw new NotSupportedException(
                    $"Unsupported CheckAllSubprocessesCompleted Mode '{args.Mode}' - only " +
                    "'AllSubprocessesAndParent' is implemented.");

            var spawned = await _tracker.CountSpawnedAsync(context.Instance.Id);
            var completed = await _tracker.CountCompletedAsync(context.Instance.Id);
            return completed >= spawned;
        }
    }
}
