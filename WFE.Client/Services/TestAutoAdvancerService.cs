using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WFE.Client.Services
{
    /// <summary>
    /// Polls the engine for Waiting instances and auto-invokes a Command on each so an
    /// evaluation run keeps flowing without manual clicking. See TestAutoAdvancerOptions'
    /// doc comment for why this is scoped as a test-harness-only convenience, not a pattern to
    /// carry into production.
    ///
    /// A multi-step workflow with several sequential Command-gated transitions will advance one
    /// step per poll cycle, not all at once - PollingIntervalMs is effectively how fast such a
    /// workflow flows through during a test run.
    /// </summary>
    public class TestAutoAdvancerService : BackgroundService
    {
        private readonly TestAutoAdvancerOptions _options;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly PacketActivityLog _log;
        private readonly ILogger<TestAutoAdvancerService> _logger;

        public TestAutoAdvancerService(
            TestAutoAdvancerOptions options, IServiceScopeFactory scopeFactory, PacketActivityLog log,
            ILogger<TestAutoAdvancerService> logger)
        {
            _options = options;
            _scopeFactory = scopeFactory;
            _log = log;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation(
                    "Test auto-advancer is disabled (TestAutoAdvancer:Enabled=false) - Waiting instances " +
                    "will need a manual command via Swagger/the dashboard.");
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await AdvanceOnceAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Test auto-advancer poll failed");
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

        private async Task AdvanceOnceAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var api = scope.ServiceProvider.GetRequiredService<IWfeApiClient>();

            var waiting = await api.GetWaitingInstancesAsync(_options.BatchSize, cancellationToken);
            if (waiting.Count == 0)
                return;

            foreach (var instance in waiting)
            {
                var commands = await api.GetAvailableCommandsAsync(instance.Id, _options.ActorId, cancellationToken);

                // Zero available commands means this instance is Waiting on a Schedule trigger,
                // not a Command - not ours to touch, leave it for WFE.Web's ScheduleWorker.
                if (commands.Count == 0)
                    continue;

                var chosen = string.IsNullOrEmpty(_options.PreferredCommandName)
                    ? commands.First()
                    : commands.FirstOrDefault(c => string.Equals(c.Title, _options.PreferredCommandName, StringComparison.Ordinal));

                if (chosen == null)
                    continue; // preferred command isn't currently available on this instance

                var result = await api.ExecuteCommandAsync(instance.Id, chosen.Title, _options.ActorId, cancellationToken);

                _log.Add(new PacketLogEntry
                {
                    Source = "AutoAdvance",
                    Tag = chosen.Title,
                    Value = $"instance #{instance.Id}",
                    Result = result
                });

                if (!result.Success)
                    _logger.LogWarning("Auto-advance failed for instance {InstanceId}, command '{Command}': {Error}",
                        instance.Id, chosen.Title, result.ErrorMessage);
            }
        }
    }
}
