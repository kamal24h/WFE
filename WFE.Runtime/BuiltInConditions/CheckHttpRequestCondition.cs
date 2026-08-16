using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using WFE.Core.Conditions;
using WFE.Core.Rules;
using WFE.Core.Runtime;

namespace WFE.Runtime.BuiltInConditions
{
    public class CheckHttpRequestArgs
    {
        /// <summary>Name of the instance parameter holding a previously-stored HTTP response
        /// body (i.e. what an HTTPRequest action with StoreResponse:true wrote).</summary>
        [JsonPropertyName("ParameterName")]
        public string ParameterName { get; set; }

        [JsonPropertyName("CompareType")]
        public string CompareType { get; set; }

        [JsonPropertyName("ResultFieldValue")]
        public string ResultFieldValue { get; set; }

        [JsonPropertyName("Separator")]
        public string Separator { get; set; } = ",";

        /// <summary>Top-level JSON property name to extract from the stored response before
        /// comparing. Leave null/empty to compare the raw stored string directly.</summary>
        [JsonPropertyName("ResultFieldName")]
        public string ResultFieldName { get; set; }

        // Your HTTPRequest.xml sample's ActionParameter also carries Url/Post/ContentType/
        // AddProcessInstanceParameters/Parameters fields on this condition. I'm deliberately
        // NOT using them here: re-issuing an HTTP call from inside a *condition* means
        // evaluating a transition has a side effect (and isn't idempotent - re-checking the
        // same transition twice would fire two HTTP requests). My assumption is those fields
        // are carried over from the designer's shared ActionParameter template rather than
        // meant to trigger a second request here - this condition only reads back whatever an
        // earlier HTTPRequest *action* already stored. Flag me if that's wrong and you actually
        // want this condition to make its own live HTTP call.
    }

    public class CheckHttpRequestCondition : IConditionExecutor
    {
        private readonly IRuleEvaluator _ruleEvaluator;

        public CheckHttpRequestCondition(IRuleEvaluator ruleEvaluator)
        {
            _ruleEvaluator = ruleEvaluator;
        }

        public string Name => "CheckHTTPRequest";

        public async Task<bool> EvaluateAsync(WorkflowExecutionContext context, string rawJsonParameters)
        {
            var args = JsonSerializer.Deserialize<CheckHttpRequestArgs>(rawJsonParameters,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var stored = await context.Parameters.GetAsync(context.Instance.Id, args.ParameterName);
            if (stored == null)
                return false;

            string actualValue;
            if (string.IsNullOrEmpty(args.ResultFieldName))
            {
                actualValue = stored;
            }
            else
            {
                using var doc = JsonDocument.Parse(stored);
                if (!doc.RootElement.TryGetProperty(args.ResultFieldName, out var element))
                    return false;
                actualValue = element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText();
            }

            var rule = new RuleDefinition
            {
                Field = "_value",
                Operator = CompareTypeMapper.Map(args.CompareType),
                Value = args.ResultFieldValue,
                Separator = args.Separator ?? ","
            };
            var singleValue = new Dictionary<string, string> { ["_value"] = actualValue };
            return _ruleEvaluator.Evaluate(new RuleGroup { Rules = new List<RuleDefinition> { rule } }, singleValue);
        }
    }
}
