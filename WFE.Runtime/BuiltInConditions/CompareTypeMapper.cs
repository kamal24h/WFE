using System;
using WFE.Core.Rules;

namespace WFE.Runtime.BuiltInConditions
{
    /// <summary>
    /// Maps the schema's CompareType string (as seen in CheckParameter/CheckHTTPRequest
    /// ActionParameter JSON) onto the rule engine's RuleOperator, so both condition executors
    /// share one mapping instead of duplicating a switch statement.
    /// </summary>
    internal static class CompareTypeMapper
    {
        public static RuleOperator Map(string compareType) => compareType switch
        {
            "Equal" => RuleOperator.Equal,
            "NotEqual" => RuleOperator.NotEqual,
            "Greater" => RuleOperator.GreaterThan,
            "GreaterOrEqual" => RuleOperator.GreaterThanOrEqual,
            "Less" => RuleOperator.LessThan,
            "LessOrEqual" => RuleOperator.LessThanOrEqual,
            "Contains" => RuleOperator.Contains,
            "In" => RuleOperator.In,
            _ => throw new NotSupportedException(
                $"Unsupported CompareType '{compareType}'. Supported: Equal, NotEqual, Greater, " +
                "GreaterOrEqual, Less, LessOrEqual, Contains, In.")
        };
    }
}
