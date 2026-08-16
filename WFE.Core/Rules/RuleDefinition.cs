using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WFE.Core.Rules
{
    public enum RuleOperator
    {
        Equal,
        NotEqual,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
        Contains,
        StartsWith,
        EndsWith,
        Between,
        In
    }

    /// <summary>
    /// A single comparison against one instance parameter (a packet's Tag/Value pair, or any
    /// other parameter set earlier in the flow). Numeric operators fall back to ordinal string
    /// comparison automatically if either side isn't parseable as a number, so the same rule
    /// works whether the sensor value is "42.5" or a status string like "RUNNING".
    /// </summary>
    public class RuleDefinition
    {
        /// <summary>Parameter name to read from the instance (e.g. the sensor Tag, or "Value").</summary>
        [JsonPropertyName("field")]
        public string Field { get; set; }

        [JsonPropertyName("operator")]
        public RuleOperator Operator { get; set; }

        [JsonPropertyName("value")]
        public string Value { get; set; }

        /// <summary>Only used by Between (upper bound) and ignored otherwise.</summary>
        [JsonPropertyName("value2")]
        public string Value2 { get; set; }

        /// <summary>Only used by In - Value holds a delimited list, delimiter configurable here.</summary>
        [JsonPropertyName("separator")]
        public string Separator { get; set; } = ",";

        [JsonPropertyName("caseSensitive")]
        public bool CaseSensitive { get; set; } = false;
    }

    public enum RuleConcatenation
    {
        And,
        Or
    }

    /// <summary>
    /// A group of rules (and/or nested groups) combined with And/Or - mirrors the
    /// ConditionsConcatenationType already present at the Transition level, but lets a single
    /// Condition/Action carry a whole boolean expression tree instead of just one comparison.
    /// </summary>
    public class RuleGroup
    {
        [JsonPropertyName("concatenation")]
        public RuleConcatenation Concatenation { get; set; } = RuleConcatenation.And;

        [JsonPropertyName("rules")]
        public List<RuleDefinition> Rules { get; set; } = new List<RuleDefinition>();

        [JsonPropertyName("groups")]
        public List<RuleGroup> Groups { get; set; } = new List<RuleGroup>();
    }
}
