using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WFE.Core.Runtime;

namespace WFE.Runtime.Scheduling
{
    /// <summary>
    /// The actual "AnotherThread" implementation: polls WfeProcessWorkItem for Pending rows and
    /// runs each spawned subprocess to completion (or to its own Waiting/Faulted point) on this
    /// background thread - independent of whatever request originally enqueued it (e.g. a
    /// packet-ingestion call, which already returned its own result before this ever runs).
    ///
    /// Each poll uses a fresh DI scope (a fresh WfeDbContext, fresh IWorkflowRuntime instance
    /// graph) rather than one long-lived scope - EF Core DbContexts aren't meant to be reused
    /// indefinitely across unrelated units of work.
    /// </summary>
    public class SubprocessWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly SubprocessWorkerOptions _options;
        private readonly ILogger<SubprocessWorker> _logger;

        public SubprocessWorker(IServiceScopeFactory scopeFactory, SubprocessWorkerOptions options, ILogger<SubprocessWorker> logger)
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
                    await ProcessBatchAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    // A batch-level failure (e.g. a transient DB outage) shouldn't kill the
                    // worker permanently - log and try again next poll.
                    _logger.LogError(ex, "SubprocessWorker batch failed");
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

        private async Task ProcessBatchAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var workItemStore = scope.ServiceProvider.GetRequiredService<IProcessWorkItemStore>();
            var runtime = scope.ServiceProvider.GetRequiredService<IWorkflowRuntime>();

            var batch = await workItemStore.ClaimBatchAsync(_options.BatchSize, cancellationToken);
            if (batch.Count == 0)
                return;

            _logger.LogInformation("SubprocessWorker claimed {Count} work item(s)", batch.Count);

            foreach (var item in batch)
            {
                try
                {
                    var parameters = string.IsNullOrEmpty(item.ParametersJson)
                        ? new Dictionary<string, string>()
                        : JsonSerializer.Deserialize<Dictionary<string, string>>(item.ParametersJson);

                    // This runs the ENTIRE subprocess (to Completed/Waiting/Faulted) here, on
                    // this background thread - that's the point: whatever enqueued this work
                    // item already returned to its own caller.
                    await runtime.StartChildInstanceAsync(
                        item.ProcessSchemeId, item.StartActivity, parameters, item.ActorId,
                        item.ParentInstanceId, item.RootInstanceId, item.ForkTransitionName, cancellationToken);

                    // "Completed" here means the spawn attempt succeeded, NOT that the child
                    // instance's business outcome was success - it might itself be Waiting or
                    // Faulted, which is a normal, separate thing to observe on that instance.
                    await workItemStore.MarkCompletedAsync(item.Id, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Subprocess work item {Id} failed to spawn", item.Id);
                    await workItemStore.MarkFaultedAsync(item.Id, ex.Message, cancellationToken);
                }
            }
        }
    }
}
