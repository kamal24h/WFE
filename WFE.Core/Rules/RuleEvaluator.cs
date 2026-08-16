using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using WFE.Core.Conditions;
using WFE.Core.Runtime;

namespace WFE.Core.Rules
{
    public interface IRuleEvaluator
    {
        bool Evaluate(RuleGroup group, System.Collections.Generic.IReadOnlyDictionary<string, string> parameters);
    }

    public class RuleEvaluator : IRuleEvaluator
    {
        public bool Evaluate(RuleGroup group, System.Collections.Generic.IReadOnlyDictionary<string, string> parameters)
        {
            if (group == null) return true;

            var ruleResults = group.Rules.Select(r => EvaluateRule(r, parameters));
            var groupResults = group.Groups.Select(g => Evaluate(g, parameters));
            var results = ruleResults.Concat(groupResults).ToList();

            if (results.Count == 0) return true;

            return group.Concatenation == RuleConcatenation.And
                ? results.All(r => r)
                : results.Any(r => r);
        }

        private bool EvaluateRule(RuleDefinition rule, System.Collections.Generic.IReadOnlyDictionary<string, string> parameters)
        {
            parameters.TryGetValue(rule.Field, out var raw);

            switch (rule.Operator)
            {
                case RuleOperator.Contains:
                    return Compare(raw, rule.Value, rule.CaseSensitive, (a, b) => a.Contains(b));
                case RuleOperator.StartsWith:
                    return Compare(raw, rule.Value, rule.CaseSensitive, (a, b) => a.StartsWith(b));
                case RuleOperator.EndsWith:
                    return Compare(raw, rule.Value, rule.CaseSensitive, (a, b) => a.EndsWith(b));
                case RuleOperator.In:
                    var options = (rule.Value ?? string.Empty).Split(
                        new[] { rule.Separator ?? "," }, StringSplitOptions.RemoveEmptyEntries);
                    return options.Any(o => ValuesEqual(raw, o, rule.CaseSensitive));
                case RuleOperator.Between:
                    return TryNumeric(raw, out var numBetween)
                           && TryNumeric(rule.Value, out var lo)
                           && TryNumeric(rule.Value2, out var hi)
                           && numBetween >= lo && numBetween <= hi;
                default:
                    return CompareOrdered(raw, rule.Value, rule.Operator, rule.CaseSensitive);
            }
        }

        private static bool Compare(string raw, string value, bool caseSensitive, Func<string, string, bool> op)
        {
            if (raw == null || value == null) return false;
            var a = caseSensitive ? raw : raw.ToUpperInvariant();
            var b = caseSensitive ? value : value.ToUpperInvariant();
            return op(a, b);
        }

        private static bool ValuesEqual(string raw, string value, bool caseSensitive) =>
            string.Equals(raw, value,
                caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);

        private static bool CompareOrdered(string raw, string value, RuleOperator op, bool caseSensitive)
        {
            // Prefer numeric comparison; fall back to string comparison so status-style
            // values (e.g. "RUNNING" vs "STOPPED") still work with GreaterThan/LessThan
            // as an ordinal comparison if that's ever meaningful for you.
            if (TryNumeric(raw, out var numA) && TryNumeric(value, out var numB))
            {
                return op switch
                {
                    RuleOperator.Equal => numA == numB,
                    RuleOperator.NotEqual => numA != numB,
                    RuleOperator.GreaterThan => numA > numB,
                    RuleOperator.GreaterThanOrEqual => numA >= numB,
                    RuleOperator.LessThan => numA < numB,
                    RuleOperator.LessThanOrEqual => numA <= numB,
                    _ => false
                };
            }

            var cmp = string.Compare(raw, value,
                caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
            return op switch
            {
                RuleOperator.Equal => cmp == 0,
                RuleOperator.NotEqual => cmp != 0,
                RuleOperator.GreaterThan => cmp > 0,
                RuleOperator.GreaterThanOrEqual => cmp >= 0,
                RuleOperator.LessThan => cmp < 0,
                RuleOperator.LessThanOrEqual => cmp <= 0,
                _ => false
            };
        }

        private static bool TryNumeric(string value, out double result) =>
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    /// <summary>
    /// Wires the rule engine into the schema's existing Condition mechanism:
    ///   &lt;Condition Type="Action" NameRef="EvaluateRule"&gt;
    ///     &lt;ActionParameter&gt;{ ...RuleGroup json... }&lt;/ActionParameter&gt;
    ///   &lt;/Condition&gt;
    /// so the designer/XML shape you already have doesn't need to change to use it.
    /// </summary>
    public class EvaluateRuleCondition : IConditionExecutor
    {
        private readonly IRuleEvaluator _evaluator;

        public EvaluateRuleCondition(IRuleEvaluator evaluator)
        {
            _evaluator = evaluator;
        }

        public string Name => "EvaluateRule";

        public async Task<bool> EvaluateAsync(WorkflowExecutionContext context, string rawJsonParameters)
        {
            var group = JsonSerializer.Deserialize<RuleGroup>(rawJsonParameters,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var parameters = await context.Parameters.GetAllAsync(context.Instance.Id);
            return _evaluator.Evaluate(group, parameters);
        }
    }
}
