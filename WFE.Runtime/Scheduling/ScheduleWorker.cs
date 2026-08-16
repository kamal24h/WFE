using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WFE.Core.Runtime;

namespace WFE.Runtime.Scheduling
{
    /// <summary>
    /// Polls for Waiting instances whose NextScheduledCheckTime has passed and resumes them via
    /// IWorkflowRuntime.ResumeScheduledAsync. General-purpose by design - the same mechanism
    /// serves a plant-floor polling loop (Interval mode) and a business-process reminder/
    /// escalation (TargetDateTime mode) equally; nothing here is packet-pipeline-specific.
    /// </summary>
    public class ScheduleWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ScheduleWorkerOptions _options;
        private readonly ILogger<ScheduleWorker> _logger;

        public ScheduleWorker(IServiceScopeFactory scopeFactory, ScheduleWorkerOptions options, ILogger<ScheduleWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessDueInstancesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ScheduleWorker poll failed");
                }

                try
                {
                    await Task.Delay(_options.PollingIntervalMs, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // normal on shutdown
                }
            }
        }

        private async Task ProcessDueInstancesAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var instanceStore = scope.ServiceProvider.GetRequiredService<IProcessInstanceStore>();
            var runtime = scope.ServiceProvider.GetRequiredService<IWorkflowRuntime>();

            var due = await instanceStore.GetDueScheduledInstancesAsync(DateTime.UtcNow, _options.BatchSize, cancellationToken);
            if (due.Count == 0)
                return;

            _logger.LogInformation("ScheduleWorker found {Count} due instance(s)", due.Count);

            foreach (var instance in due)
            {
                try
                {
                    await runtime.ResumeScheduledAsync(instance.Id, cancellationToken);
                }
                catch (Exception ex)
                {
                    // A concurrency conflict here (e.g. a human fired a Command on the same
                    // instance at the same moment) is expected and not fatal - log and move on;
                    // if the instance is still relevant, it'll be picked up correctly next poll.
                    _logger.LogWarning(ex, "ScheduleWorker failed to resume instance {InstanceId}", instance.Id);
                }
            }
        }
    }
}
