using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WFE.Core.Conditions;
using WFE.Core.Runtime;
using WFE.Core.Schema;

namespace WFE.Runtime
{
    public class AutoTransitionResolution
    {
        /// <summary>The single transition (if any) that determines this instance's own
        /// continued path - resolved via the normal first-match/Otherwise rules, EXCLUDING
        /// Fork/Start transitions (which are parallel side-branches, not alternatives).</summary>
        public TransitionDefinitionXml MainTransition { get; init; }

        /// <summary>Every Fork+Start transition whose conditions were satisfied - zero or
        /// more can fire simultaneously alongside MainTransition, since each spawns an
        /// independent subprocess rather than competing for "the" next step.</summary>
        public System.Collections.Generic.IReadOnlyList<TransitionDefinitionXml> ForkStartTransitions { get; init; }
            = System.Array.Empty<TransitionDefinitionXml>();

        public bool HasMainTransition => MainTransition != null;
    }

    /// <summary>
    /// Pure condition-resolution logic - no persistence, no action execution. WorkflowRuntime
    /// owns "what happens after a transition fires" (history logging, activity actions, etc);
    /// this class only answers "which transition, if any, fires right now".
    /// </summary>
    public class TransitionEngine
    {
        private readonly ConditionExecutorRegistry _conditions;
        private readonly ExpressionConditionEvaluator _expressionEvaluator;

        public TransitionEngine(ConditionExecutorRegistry conditions, ExpressionConditionEvaluator expressionEvaluator)
        {
            _conditions = conditions;
            _expressionEvaluator = expressionEvaluator;
        }

        /// <summary>
        /// Evaluates Auto-triggered outbound transitions from the current activity, in XML
        /// declaration order. Fork+Start transitions are evaluated independently (any number
        /// can match - see AutoTransitionResolution.ForkStartTransitions). Among the REMAINING
        /// candidates (including Fork+Finalize transitions, which ARE a subprocess instance's
        /// normal primary path, not a side-branch), two passes: transitions with at least one
        /// non-Otherwise condition are tried first (first satisfied wins); only if none of
        /// those match do pure-Otherwise transitions get considered (first in order wins).
        /// Command- and Schedule-triggered transitions are never resolved here.
        /// </summary>
        public async Task<AutoTransitionResolution> ResolveAutoTransitionAsync(
            ResolvedProcessSchema schema, WorkflowExecutionContext context, CancellationToken cancellationToken = default)
        {
            var outbound = schema.OutboundTransitions(context.Instance.Activity);

            var autoCandidates = outbound
                .Where(t => t.Triggers.Any(tr => string.Equals(tr.Type, "Auto", StringComparison.Ordinal)))
                .ToList();

            var forkStartCandidates = autoCandidates
                .Where(t => t.IsForkValue && string.Equals(t.SubprocessInOutDefinition, "Start", StringComparison.Ordinal))
                .ToList();

            var matchedForks = new List<TransitionDefinitionXml>();
            foreach (var t in forkStartCandidates)
                if (await EvaluateTransitionConditionsAsync(t, schema, context, cancellationToken))
                    matchedForks.Add(t);

            var mainCandidates = autoCandidates.Except(forkStartCandidates).ToList();
            var primary = mainCandidates.Where(t => !IsPureOtherwise(t)).ToList();
            var fallback = mainCandidates.Where(IsPureOtherwise).ToList();

            TransitionDefinitionXml mainMatch = null;
            foreach (var t in primary)
            {
                if (await EvaluateTransitionConditionsAsync(t, schema, context, cancellationToken))
                {
                    mainMatch = t;
                    break;
                }
            }
            if (mainMatch == null && fallback.Count > 0)
                mainMatch = fallback[0];

            return new AutoTransitionResolution { MainTransition = mainMatch, ForkStartTransitions = matchedForks };
        }

        /// <summary>Finds the outbound transition from the current activity whose trigger is
        /// Command NameRef==commandName, or null if the command isn't available here.</summary>
        public TransitionDefinitionXml FindCommandTransition(
            ResolvedProcessSchema schema, string activity, string commandName)
        {
            return schema.OutboundTransitions(activity).FirstOrDefault(t =>
                t.Triggers.Any(tr =>
                    string.Equals(tr.Type, "Command", StringComparison.Ordinal) &&
                    string.Equals(tr.NameRef, commandName, StringComparison.Ordinal)));
        }

        /// <summary>All Command-triggered transitions available from the given activity - the
        /// basis for the "what can I do right now" command list.</summary>
        public IReadOnlyList<TransitionDefinitionXml> GetCommandTransitions(ResolvedProcessSchema schema, string activity)
        {
            return schema.OutboundTransitions(activity)
                .Where(t => t.Triggers.Any(tr => string.Equals(tr.Type, "Command", StringComparison.Ordinal)))
                .ToList();
        }

        /// <summary>All Schedule-triggered transitions available from the given activity -
        /// used to compute an instance's NextScheduledCheckTime when it parks in Waiting.</summary>
        public IReadOnlyList<TransitionDefinitionXml> GetScheduleTransitions(ResolvedProcessSchema schema, string activity)
        {
            return schema.OutboundTransitions(activity)
                .Where(t => t.Triggers.Any(tr => string.Equals(tr.Type, "Schedule", StringComparison.Ordinal)))
                .ToList();
        }

        /// <summary>Evaluates Schedule-triggered outbound transitions the same way Auto ones
        /// are evaluated (first-match/Otherwise two-pass) - called by ScheduleWorker once an
        /// instance's NextScheduledCheckTime has passed, never during the normal Auto loop.</summary>
        public async Task<TransitionDefinitionXml> ResolveScheduleTransitionAsync(
            ResolvedProcessSchema schema, WorkflowExecutionContext context, CancellationToken cancellationToken = default)
        {
            var scheduleCandidates = GetScheduleTransitions(schema, context.Instance.Activity);
            var primary = scheduleCandidates.Where(t => !IsPureOtherwise(t)).ToList();
            var fallback = scheduleCandidates.Where(IsPureOtherwise).ToList();

            foreach (var t in primary)
                if (await EvaluateTransitionConditionsAsync(t, schema, context, cancellationToken))
                    return t;

            return fallback.Count > 0 ? fallback[0] : null;
        }

        /// <summary>Public so WorkflowRuntime can reuse it for explicit Command invocations -
        /// a Command transition can still carry real Conditions (see DirectTransitionsWithApproval.xml),
        /// they just aren't auto-evaluated until the command is invoked.</summary>
        public async Task<bool> EvaluateTransitionConditionsAsync(
            TransitionDefinitionXml transition, ResolvedProcessSchema schema, WorkflowExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            if (transition.Conditions.Count == 0)
                return true;

            var results = new List<bool>(transition.Conditions.Count);
            foreach (var condition in transition.Conditions)
                results.Add(await EvaluateSingleConditionAsync(condition, schema, context, cancellationToken));

            var isOr = string.Equals(transition.ConditionsConcatenationType, "Or", StringComparison.OrdinalIgnoreCase);
            return isOr ? results.Any(r => r) : results.All(r => r);
        }

        private static bool IsPureOtherwise(TransitionDefinitionXml t) =>
            t.Conditions.Count == 1 && string.Equals(t.Conditions[0].Type, "Otherwise", StringComparison.Ordinal);

        private async Task<bool> EvaluateSingleConditionAsync(
            ConditionXml condition, ResolvedProcessSchema schema, WorkflowExecutionContext context, CancellationToken cancellationToken)
        {
            bool result;
            switch (condition.Type)
            {
                case "Always":
                case "Otherwise":
                    result = true;
                    break;

                case "Expression":
                    var parameters = await context.Parameters.GetAllAsync(context.Instance.Id);
                    result = _expressionEvaluator.Evaluate(condition.Expression, parameters);
                    break;

                case "Action":
                    var executor = _conditions.Resolve(schema, condition.NameRef);
                    result = await executor.EvaluateAsync(context, condition.ActionParameter);
                    break;

                default:
                    throw new NotSupportedException($"Unsupported Condition Type '{condition.Type}'.");
            }

            return condition.IsInverted ? !result : result;
        }
    }
}
