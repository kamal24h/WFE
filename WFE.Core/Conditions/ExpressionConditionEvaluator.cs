using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace WFE.Core.Conditions
{
    /// <summary>
    /// Handles exactly the shapes seen in the schema samples:
    ///   "true" / "false"                     - literal
    ///   "@ParamName"                          - truthy check (non-empty, non-"false"/"0")
    ///   "@ParamName OP literal"               - OP in > >= &lt; &lt;= == !=
    /// Deliberately NOT a general expression/scripting engine - no arbitrary code execution,
    /// so a compromised or malformed schema can't do anything beyond compare a parameter.
    /// Extend the regex/switch here if you need compound (&amp;&amp;/||) expressions later;
    /// resist the urge to swap this for eval()-style dynamic compilation.
    /// </summary>
    public class ExpressionConditionEvaluator
    {
        private static readonly Regex ComparisonPattern = new Regex(
            @"^\s*@(?<param>[A-Za-z_][A-Za-z0-9_]*)\s*(?<op>>=|<=|==|!=|>|<)\s*(?<literal>.+?)\s*$",
            RegexOptions.Compiled);

        private static readonly Regex TruthyPattern = new Regex(
            @"^\s*@(?<param>[A-Za-z_][A-Za-z0-9_]*)\s*$", RegexOptions.Compiled);

        public bool Evaluate(string expression, System.Collections.Generic.IReadOnlyDictionary<string, string> parameters)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return false;

            var trimmed = expression.Trim();

            if (string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase)) return false;

            var cmp = ComparisonPattern.Match(trimmed);
            if (cmp.Success)
            {
                parameters.TryGetValue(cmp.Groups["param"].Value, out var raw);
                var literal = cmp.Groups["literal"].Value.Trim().Trim('"');
                var op = cmp.Groups["op"].Value;
                return CompareValues(raw, literal, op);
            }

            var truthy = TruthyPattern.Match(trimmed);
            if (truthy.Success)
            {
                parameters.TryGetValue(truthy.Groups["param"].Value, out var raw);
                return !string.IsNullOrEmpty(raw)
                       && !string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase)
                       && raw != "0";
            }

            throw new NotSupportedException(
                $"Expression '{expression}' is not in a supported form (literal, @Param, or @Param OP literal).");
        }

        private static bool CompareValues(string raw, string literal, string op)
        {
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var numA)
                && double.TryParse(literal, NumberStyles.Float, CultureInfo.InvariantCulture, out var numB))
            {
                return op switch
                {
                    ">" => numA > numB,
                    ">=" => numA >= numB,
                    "<" => numA < numB,
                    "<=" => numA <= numB,
                    "==" => numA == numB,
                    "!=" => numA != numB,
                    _ => false
                };
            }

            var strCmp = string.CompareOrdinal(raw, literal);
            return op switch
            {
                ">" => strCmp > 0,
                ">=" => strCmp >= 0,
                "<" => strCmp < 0,
                "<=" => strCmp <= 0,
                "==" => strCmp == 0,
                "!=" => strCmp != 0,
                _ => false
            };
        }
    }
}
