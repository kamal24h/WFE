using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WFE.Core.Runtime;
using WFE.Core.Schema;
using WFE.Models;

namespace WFE.Runtime
{
    public class WorkflowRuntime : IWorkflowRuntime
    {
        private readonly IProcessSchemeProvider _schemeProvider;
        private readonly IProcessInstanceStore _instanceStore;
        private readonly IWorkflowParameterStore _parameters;
        private readonly IProcessWorkItemStore _workItems;
        private readonly TransitionEngine _transitionEngine;
        private readonly ActionExecutorRegistry _actions;
        private readonly ActionExecutionPolicyOptions _policy;
        private readonly WorkflowRuntimeOptions _runtimeOptions;
        private readonly ScheduleWorkerOptions _scheduleOptions;
        private readonly CommandService _commandService;
        private readonly ILogger<WorkflowRuntime> _logger;

        public WorkflowRuntime(
            IProcessSchemeProvider schemeProvider,
            IProcessInstanceStore instanceStore,
            IWorkflowParameterStore parameters,
            IProcessWorkItemStore workItems,
            TransitionEngine transitionEngine,
            ActionExecutorRegistry actions,
            ActionExecutionPolicyOptions policy,
            WorkflowRuntimeOptions runtimeOptions,
            ScheduleWorkerOptions scheduleOptions,
            CommandService commandService,
            ILogger<WorkflowRuntime> logger)
        {
            _schemeProvider = schemeProvider;
            _instanceStore = instanceStore;
            _parameters = parameters;
            _workItems = workItems;
            _transitionEngine = transitionEngine;
            _actions = actions;
            _policy = policy;
            _runtimeOptions = runtimeOptions;
            _scheduleOptions = scheduleOptions;
            _commandService = commandService;
            _logger = logger;
        }

        public async Task<WorkflowExecutionResult> StartInstanceAsync(
            long processSchemeId,
            IReadOnlyDictionary<string, string> initialParameters,
            string actorId,
            CancellationToken cancellationToken = default)
        {
            var (scheme, resolved) = await _schemeProvider.GetAsync(processSchemeId, cancellationToken);
            return await CreateAndRunAsync(scheme, resolved, resolved.InitialActivity, initialParameters,
                actorId, parentInstanceId: null, rootInstanceId: null, forkTransitionName: null, cancellationToken);
        }

        public Task<WorkflowExecutionResult> ProcessPacketAsync(
            long processSchemeId, WfePacket packet, string actorId, CancellationToken cancellationToken = default)
        {
            var parameters = new Dictionary<string, string>
            {
                ["Tag"] = packet.Tag,
                ["Value"] = packet.Value,
                ["Timestamp"] = packet.Timestamp.ToString("O")
            };
            if (packet.Metadata != null)
                foreach (var kvp in packet.Metadata)
                    parameters[kvp.Key] = kvp.Value;

            return StartInstanceAsync(processSchemeId, parameters, actorId, cancellationToken);
        }

        public async Task<WorkflowExecutionResult> StartChildInstanceAsync(
            long processSchemeId,
            string activityName,
            IReadOnlyDictionary<string, string> parameters,
            string actorId,
            long parentInstanceId,
            long rootInstanceId,
            string forkTransitionName,
            CancellationToken cancellationToken = default)
        {
            var (scheme, resolved) = await _schemeProvider.GetAsync(processSchemeId, cancellationToken);
            if (!resolved.ActivitiesByName.TryGetValue(activityName, out var activityDef))
                throw new InvalidOperationException(
                    $"Activity '{activityName}' not found in scheme {processSchemeId} - cannot start a subprocess there.");

            return await CreateAndRunAsync(scheme, resolved, activityDef, parameters, actorId,
                parentInstanceId, rootInstanceId, forkTransitionName, cancellationToken);
        }

        private async Task<WorkflowExecutionResult> CreateAndRunAsync(
            WfeProcessScheme scheme, ResolvedProcessSchema resolved, ActivityDefinitionXml startActivity,
            IReadOnlyDictionary<string, string> initialParameters, string actorId,
            long? parentInstanceId, long? rootInstanceId, string forkTransitionName,
            CancellationToken cancellationToken)
        {
            var instance = new WfeProcessInstance
            {
                ProcessSchemeId = scheme.Id,
                Activity = startActivity.Name,
                State = startActivity.State,
                Status = "Running",
                CreationDateTime = DateTime.UtcNow,
                ParentInstanceId = parentInstanceId,
                RootInstanceId = rootInstanceId,
                ForkTransitionName = forkTransitionName
            };
            instance = await _instanceStore.CreateAsync(instance, cancellationToken);

            if (initialParameters != null)
                foreach (var kvp in initialParameters)
                    await _parameters.SetAsync(instance.Id, kvp.Key, kvp.Value);

            _logger.LogInformation(
                "Instance {InstanceId} started on scheme {SchemeId} at {Activity} (parent {ParentId})",
                instance.Id, scheme.Id, instance.Activity, parentInstanceId);

            return await RunAutoLoopAsync(scheme, resolved, instance, actorId, cancellationToken);
        }

        public async Task<WorkflowExecutionResult> ExecuteCommandAsync(
            long instanceId,
            string commandName,
            string actorId,
            IReadOnlyDictionary<string, string> parameters = null,
            CancellationToken cancellationToken = default)
        {
            var instance = await _instanceStore.GetAsync(instanceId, cancellationToken);
            if (instance == null)
                return WorkflowExecutionResult.NotFound(instanceId);

            if (!string.Equals(instance.Status, "Waiting", StringComparison.Ordinal))
            {
                var r = ToResult(instance);
                r.Message = $"Instance is not waiting for a command (current status: {instance.Status}).";
                return r;
            }

            var (scheme, resolved) = await _schemeProvider.GetAsync(instance.ProcessSchemeId, cancellationToken);

            var transition = _transitionEngine.FindCommandTransition(resolved, instance.Activity, commandName);
            if (transition == null)
            {
                var r = ToResult(instance);
                r.Message = $"Command '{commandName}' is not available from activity '{instance.Activity}'.";
                return r;
            }

            if (parameters != null)
                foreach (var kvp in parameters)
                    await _parameters.SetAsync(instance.Id, kvp.Key, kvp.Value);

            var context = new WorkflowExecutionContext(instance, _parameters, actorId, cancellationToken);
            var conditionsSatisfied = await _transitionEngine.EvaluateTransitionConditionsAsync(transition, resolved, context, cancellationToken);
            if (!conditionsSatisfied)
            {
                var r = ToResult(instance);
                r.Message = $"Command '{commandName}' conditions are not currently satisfied.";
                return r;
            }

            ApplyTransitionFields(instance, transition, resolved);
            var history = scheme.TrackHistory ? BuildHistory(instance, transition, actorId) : null;
            await _instanceStore.SaveActivityTransitionAsync(instance, history, scheme.TrackHistory, cancellationToken);

            _logger.LogInformation("Instance {InstanceId} executed command {Command} -> {Activity}",
                instance.Id, commandName, instance.Activity);

            return await RunAutoLoopAsync(scheme, resolved, instance, actorId, cancellationToken);
        }

        public async Task<WorkflowExecutionResult> ResumeScheduledAsync(
            long instanceId, CancellationToken cancellationToken = default)
        {
            var instance = await _instanceStore.GetAsync(instanceId, cancellationToken);
            if (instance == null)
                return WorkflowExecutionResult.NotFound(instanceId);

            // Not an error - a human command or another scheduled check may have already
            // moved this instance on since ScheduleWorker read it as due.
            if (!string.Equals(instance.Status, "Waiting", StringComparison.Ordinal))
                return ToResult(instance);

            var (scheme, resolved) = await _schemeProvider.GetAsync(instance.ProcessSchemeId, cancellationToken);
            const string scheduleActorId = "system:scheduler";

            var context = new WorkflowExecutionContext(instance, _parameters, scheduleActorId, cancellationToken);
            var transition = await _transitionEngine.ResolveScheduleTransitionAsync(resolved, context, cancellationToken);

            if (transition == null)
            {
                // Due, but conditions alongside the schedule (if any) aren't satisfied yet -
                // push the check forward rather than re-evaluating every single poll.
                var retryAt = DateTime.UtcNow.AddSeconds(_scheduleOptions.RetryIntervalSecondsIfNotFired);
                await _instanceStore.UpdateNextScheduledCheckTimeAsync(instance.Id, retryAt, cancellationToken);
                var r = ToResult(instance);
                r.Message = "Schedule check ran but no Schedule transition's conditions were satisfied yet.";
                return r;
            }

            ApplyTransitionFields(instance, transition, resolved);
            var history = scheme.TrackHistory ? BuildHistory(instance, transition, scheduleActorId) : null;
            await _instanceStore.SaveActivityTransitionAsync(instance, history, scheme.TrackHistory, cancellationToken);

            _logger.LogInformation("Instance {InstanceId} fired scheduled transition {Transition} -> {Activity}",
                instance.Id, transition.Name, instance.Activity);

            return await RunAutoLoopAsync(scheme, resolved, instance, scheduleActorId, cancellationToken);
        }

        /// <summary>Null if the current activity has no Schedule-triggered outbound
        /// transitions; otherwise the earliest of all their computed next-fire times (so
        /// whichever would fire soonest determines when ScheduleWorker re-checks).</summary>
        private async Task<DateTime?> ComputeNextScheduledCheckTimeAsync(
            ResolvedProcessSchema resolved, WfeProcessInstance instance, string actorId, CancellationToken cancellationToken)
        {
            var scheduleTransitions = _transitionEngine.GetScheduleTransitions(resolved, instance.Activity);
            if (scheduleTransitions.Count == 0)
                return null;

            var parameters = await _parameters.GetAllAsync(instance.Id);
            var now = DateTime.UtcNow;
            DateTime? earliest = null;

            foreach (var t in scheduleTransitions)
            {
                var scheduleTrigger = t.Triggers.First(tr => string.Equals(tr.Type, "Schedule", StringComparison.Ordinal));
                var config = JsonSerializer.Deserialize<ScheduleTriggerConfig>(scheduleTrigger.ScheduleParameter,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                var next = ScheduleCalculator.ComputeNextFireTime(config, parameters, now);
                if (earliest == null || next < earliest)
                    earliest = next;
            }

            return earliest;
        }

        public async Task<IReadOnlyList<WfeCommand>> GetAvailableCommandsAsync(
            long instanceId, string actorId, CancellationToken cancellationToken = default)
        {
            var instance = await _instanceStore.GetAsync(instanceId, cancellationToken);
            if (instance == null) return Array.Empty<WfeCommand>();

            var (_, resolved) = await _schemeProvider.GetAsync(instance.ProcessSchemeId, cancellationToken);
            return _commandService.BuildAvailableCommands(resolved, instance, actorId);
        }

        // --- core loop ---
        private async Task<WorkflowExecutionResult> RunAutoLoopAsync(
            WfeProcessScheme scheme, ResolvedProcessSchema resolved, WfeProcessInstance instance,
            string actorId, CancellationToken cancellationToken)
        {
            var hops = 0;

            while (true)
            {
                var activityDef = resolved.ActivitiesByName[instance.Activity];

                var (success, error) = await ExecuteActivityActionsAsync(activityDef, resolved, instance, actorId, cancellationToken);
                if (!success)
                    return await FaultAsync(scheme, instance, error, cancellationToken);

                if (activityDef.IsFinalValue)
                {
                    instance.Status = "Completed";
                    instance.CompletionDateTime = DateTime.UtcNow;
                    await _instanceStore.SaveActivityTransitionAsync(instance, null, scheme.TrackHistory, cancellationToken);
                    _logger.LogInformation("Instance {InstanceId} completed at {Activity}", instance.Id, instance.Activity);
                    return ToResult(instance);
                }

                if (++hops > _runtimeOptions.MaxAutoHops)
                    return await FaultAsync(scheme, instance,
                        $"Exceeded max auto-transition hop count ({_runtimeOptions.MaxAutoHops}) - likely an infinite transition loop in the schema.",
                        cancellationToken);

                var context = new WorkflowExecutionContext(instance, _parameters, actorId, cancellationToken);
                var resolution = await _transitionEngine.ResolveAutoTransitionAsync(resolved, context, cancellationToken);

                foreach (var forkTransition in resolution.ForkStartTransitions)
                {
                    try
                    {
                        await EnqueueForkAsync(instance, forkTransition, actorId, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        return await FaultAsync(scheme, instance,
                            $"Failed to enqueue subprocess for transition '{forkTransition.Name}': {ex.Message}",
                            cancellationToken);
                    }
                }

                if (!resolution.HasMainTransition)
                {
                    instance.Status = "Waiting";
                    instance.NextScheduledCheckTime = await ComputeNextScheduledCheckTimeAsync(resolved, instance, actorId, cancellationToken);
                    await _instanceStore.SaveActivityTransitionAsync(instance, null, scheme.TrackHistory, cancellationToken);

                    var result = ToResult(instance);
                    result.AvailableCommandNames = _transitionEngine
                        .GetCommandTransitions(resolved, instance.Activity)
                        .SelectMany(t => t.Triggers.Where(tr => tr.Type == "Command").Select(tr => tr.NameRef))
                        .ToList();
                    return result;
                }

                var mainTransition = resolution.MainTransition;

                // A Fork+Finalize transition is a subprocess instance's own terminal step, not
                // a normal hop - it merges parameters into the parent and stops here regardless
                // of whether the "To" activity is marked IsFinal (see ApplyFinalizeAsync).
                if (mainTransition.IsForkValue &&
                    string.Equals(mainTransition.SubprocessInOutDefinition, "Finalize", StringComparison.Ordinal))
                {
                    return await ApplyFinalizeAsync(scheme, resolved, instance, mainTransition, cancellationToken);
                }

                ApplyTransitionFields(instance, mainTransition, resolved);
                var history = scheme.TrackHistory ? BuildHistory(instance, mainTransition, actorId) : null;
                await _instanceStore.SaveActivityTransitionAsync(instance, history, scheme.TrackHistory, cancellationToken);
                // loop continues: next iteration executes the new activity's actions
            }
        }

        private async Task EnqueueForkAsync(
            WfeProcessInstance instance, TransitionDefinitionXml transition, string actorId, CancellationToken cancellationToken)
        {
            if (!string.Equals(transition.SubprocessStartupType, "AnotherThread", StringComparison.Ordinal))
                throw new NotSupportedException(
                    $"Unsupported SubprocessStartupType '{transition.SubprocessStartupType}' on transition " +
                    $"'{transition.Name}' - only 'AnotherThread' (async, via the work-item queue) is implemented.");

            if (!string.Equals(transition.SubprocessStartupParameterCopyStrategy, "CopyAll", StringComparison.Ordinal))
                throw new NotSupportedException(
                    $"Unsupported SubprocessStartupParameterCopyStrategy " +
                    $"'{transition.SubprocessStartupParameterCopyStrategy}' on transition '{transition.Name}' - " +
                    "only 'CopyAll' is implemented.");

            var parameters = await _parameters.GetAllAsync(instance.Id);
            var workItem = new WfeProcessWorkItem
            {
                ProcessSchemeId = instance.ProcessSchemeId,
                ParentInstanceId = instance.Id,
                RootInstanceId = instance.RootInstanceId ?? instance.Id,
                StartActivity = transition.To,
                ForkTransitionName = transition.Name,
                ActorId = actorId,
                ParametersJson = JsonSerializer.Serialize(parameters)
            };
            await _workItems.EnqueueAsync(workItem, cancellationToken);

            _logger.LogInformation(
                "Enqueued subprocess for instance {InstanceId}, transition {Transition} -> activity {Activity}",
                instance.Id, transition.Name, transition.To);
        }

        private async Task<WorkflowExecutionResult> ApplyFinalizeAsync(
            WfeProcessScheme scheme, ResolvedProcessSchema resolved, WfeProcessInstance instance,
            TransitionDefinitionXml transition, CancellationToken cancellationToken)
        {
            if (instance.ParentInstanceId == null)
                return await FaultAsync(scheme, instance,
                    $"Transition '{transition.Name}' is a Fork+Finalize transition but this instance has no " +
                    "ParentInstanceId to merge into.", cancellationToken);

            if (!string.Equals(transition.SubprocessFinalizeParameterMergeStrategy, "OverwriteAllNulls", StringComparison.Ordinal))
                return await FaultAsync(scheme, instance,
                    $"Unsupported SubprocessFinalizeParameterMergeStrategy " +
                    $"'{transition.SubprocessFinalizeParameterMergeStrategy}' on transition '{transition.Name}' - " +
                    "only 'OverwriteAllNulls' is implemented.", cancellationToken);

            var parentId = instance.ParentInstanceId.Value;
            var childParams = await _parameters.GetAllAsync(instance.Id);
            foreach (var kvp in childParams)
            {
                var existing = await _parameters.GetAsync(parentId, kvp.Key);
                if (existing == null)
                    await _parameters.SetAsync(parentId, kvp.Key, kvp.Value);
            }

            ApplyTransitionFields(instance, transition, resolved);
            instance.Status = "Completed";
            instance.CompletionDateTime = DateTime.UtcNow;
            await _instanceStore.SaveActivityTransitionAsync(instance, null, scheme.TrackHistory, cancellationToken);

            _logger.LogInformation("Subprocess instance {InstanceId} finalized, merged into parent {ParentId}",
                instance.Id, parentId);
            return ToResult(instance);
        }

        private async Task<(bool Success, string Error)> ExecuteActivityActionsAsync(
            ActivityDefinitionXml activityDef, ResolvedProcessSchema resolved, WfeProcessInstance instance,
            string actorId, CancellationToken cancellationToken)
        {
            if (activityDef.Implementation == null || activityDef.Implementation.ActionRefs.Count == 0)
                return (true, null);

            var context = new WorkflowExecutionContext(instance, _parameters, actorId, cancellationToken);

            foreach (var actionRef in activityDef.Implementation.ActionRefs.OrderBy(a => a.Order))
            {
                var executor = _actions.Resolve(resolved, actionRef.NameRef);
                var (maxRetries, retryDelayMs) = _policy.ResolveFor(actionRef.NameRef);

                var attempt = 0;
                while (true)
                {
                    try
                    {
                        await executor.ExecuteAsync(context, actionRef.ActionParameter);
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (attempt < maxRetries)
                        {
                            attempt++;
                            _logger.LogWarning(ex,
                                "Action {Action} failed on instance {InstanceId} (attempt {Attempt}/{Max}), retrying",
                                actionRef.NameRef, instance.Id, attempt, maxRetries);
                            if (retryDelayMs > 0)
                                await Task.Delay(retryDelayMs, cancellationToken);
                            continue;
                        }

                        _logger.LogError(ex, "Action {Action} failed on instance {InstanceId}, halting instance",
                            actionRef.NameRef, instance.Id);
                        return (false, $"Action '{actionRef.NameRef}' failed: {ex.Message}");
                    }
                }
            }

            return (true, null);
        }

        private async Task<WorkflowExecutionResult> FaultAsync(
            WfeProcessScheme scheme, WfeProcessInstance instance, string reason, CancellationToken cancellationToken)
        {
            instance.Status = "Faulted";
            instance.FaultReason = reason;
            instance.CompletionDateTime = DateTime.UtcNow;
            await _instanceStore.SaveActivityTransitionAsync(instance, null, scheme.TrackHistory, cancellationToken);
            _logger.LogError("Instance {InstanceId} faulted: {Reason}", instance.Id, reason);
            return ToResult(instance);
        }

        private static void ApplyTransitionFields(
            WfeProcessInstance instance, TransitionDefinitionXml transition, ResolvedProcessSchema resolved)
        {
            instance.PreviousActivity = instance.Activity;
            instance.PreviousState = instance.State;
            instance.Activity = transition.To;
            instance.State = resolved.ActivitiesByName[transition.To].State;
            instance.Status = "Running";
        }

        private static WfeProcessTransitionHistory BuildHistory(
            WfeProcessInstance instance, TransitionDefinitionXml transition, string actorId) => new()
        {
            ProcessInstanceId = instance.Id,
            ActorId = actorId,
            FromActivity = instance.PreviousActivity,
            ToActivity = instance.Activity,
            FromState = instance.PreviousState,
            ToState = instance.State,
            TransitionName = transition.Name,
            StartTransitionTime = DateTime.UtcNow
        };

        private static WorkflowExecutionResult ToResult(WfeProcessInstance instance) => new()
        {
            InstanceId = instance.Id,
            Status = Enum.Parse<WorkflowInstanceStatus>(instance.Status),
            Activity = instance.Activity,
            State = instance.State,
            FaultReason = instance.FaultReason
        };
    }
}
