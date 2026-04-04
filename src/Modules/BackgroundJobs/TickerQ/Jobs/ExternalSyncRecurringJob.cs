using BackgroundJobs.Logging;
using Microsoft.Extensions.Logging;
using TickerQ.Utilities.Base;

namespace BackgroundJobs.TickerQ.Jobs;

public sealed class ExternalSyncRecurringJob
{
    private readonly IDistributedJobCoordinator _coordinator;
    private readonly ILogger<ExternalSyncRecurringJob> _logger;
    private readonly IExternalIntegrationSyncService _syncService;

    public ExternalSyncRecurringJob(
        IExternalIntegrationSyncService syncService,
        IDistributedJobCoordinator coordinator,
        ILogger<ExternalSyncRecurringJob> logger
    )
    {
        _syncService = syncService;
        _coordinator = coordinator;
        _logger = logger;
    }

    [TickerFunction(TickerQFunctionNames.ExternalSync)]
    public Task ExecuteAsync(TickerFunctionContext context, CancellationToken ct)
    {
        return _coordinator.ExecuteIfLeaderAsync(
            TickerQFunctionNames.ExternalSync,
            async token =>
            {
                _logger.ExecutingExternalSyncRecurringJob(context.Id);
                await _syncService.SynchronizeAsync(token);
            },
            ct
        );
    }
}
