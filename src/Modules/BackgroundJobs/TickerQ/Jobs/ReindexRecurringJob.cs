using BackgroundJobs.Logging;
using Microsoft.Extensions.Logging;
using TickerQ.Utilities.Base;

namespace BackgroundJobs.TickerQ.Jobs;

public sealed class ReindexRecurringJob
{
    private readonly IDistributedJobCoordinator _coordinator;
    private readonly ILogger<ReindexRecurringJob> _logger;
    private readonly IReindexService _reindexService;

    public ReindexRecurringJob(
        IReindexService reindexService,
        IDistributedJobCoordinator coordinator,
        ILogger<ReindexRecurringJob> logger
    )
    {
        _reindexService = reindexService;
        _coordinator = coordinator;
        _logger = logger;
    }

    [TickerFunction(TickerQFunctionNames.Reindex)]
    public Task ExecuteAsync(TickerFunctionContext context, CancellationToken ct)
    {
        return _coordinator.ExecuteIfLeaderAsync(
            TickerQFunctionNames.Reindex,
            async token =>
            {
                _logger.ExecutingReindexRecurringJob(context.Id);
                await _reindexService.ReindexFullTextSearchAsync(token);
            },
            ct
        );
    }
}
