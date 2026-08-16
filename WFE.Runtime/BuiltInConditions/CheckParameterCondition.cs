using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using WFE.Core.Conditions;
using WFE.Core.Rules;
using WFE.Core.Runtime;

namespace WFE.Runtime.BuiltInConditions
{
    public class CheckParameterArgs
    {
        [JsonPropertyName("CompareType")]
        public string CompareType { get; set; }

        [JsonPropertyName("Separator")]
        public string Separator { get; set; } = ",";

        // Your sample XMLs use BOTH keys across different exports (FileWriteRead.xml uses
        // "Parameter", the others use "ParameterName") - support whichever is present rather
        // than picking one and silently breaking the other export.
        [JsonPropertyName("Parameter")]
        public string Parameter { get; set; }

        [JsonPropertyName("ParameterName")]
        public string ParameterName { get; set; }

        [JsonPropertyName("Value")]
        public string Value { get; set; }

        [JsonPropertyName("ForRootProcess")]
        public bool ForRootProcess { get; set; }

        [JsonIgnore]
        public string EffectiveParameterName => !string.IsNullOrEmpty(ParameterName) ? ParameterName : Parameter;
    }

    /// <summary>
    /// &lt;Condition Type="Action" NameRef="CheckParameter"&gt; - compares a named instance
    /// parameter against a literal value. Delegates the actual comparison to the rule engine
    /// (RuleEvaluator) rather than reimplementing comparison logic here.
    /// </summary>
    public class CheckParameterCondition : IConditionExecutor
    {
        private readonly IRuleEvaluator _ruleEvaluator;

        public CheckParameterCondition(IRuleEvaluator ruleEvaluator)
        {
            _ruleEvaluator = ruleEvaluator;
        }

        public string Name => "CheckParameter";

        public async Task<bool> EvaluateAsync(WorkflowExecutionContext context, string rawJsonParameters)
        {
            var args = JsonSerializer.Deserialize<CheckParameterArgs>(rawJsonParameters,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var rule = new RuleDefinition
            {
                Field = args.EffectiveParameterName,
                Operator = CompareTypeMapper.Map(args.CompareType),
                Value = args.Value,
                Separator = args.Separator ?? ","
            };

            // TODO(Phase 3 - subprocesses): honor args.ForRootProcess once a parent/root
            // instance chain exists. Every instance is its own root today.
            var parameters = await context.Parameters.GetAllAsync(context.Instance.Id);
            return _ruleEvaluator.Evaluate(new RuleGroup { Rules = new List<RuleDefinition> { rule } }, parameters);
        }
    }
}
