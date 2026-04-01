using BackgroundJobs.Application.Services;
using Microsoft.Extensions.Logging;
using SharedKernel.Application.BackgroundJobs;
using TickerQ.Utilities.Base;

namespace BackgroundJobs.Infrastructure.TickerQ.Jobs;

public sealed class ReindexRecurringJob
{
    private readonly IReindexService _reindexService;
    private readonly IDistributedJobCoordinator _coordinator;
    private readonly ILogger<ReindexRecurringJob> _logger;

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
    public Task ExecuteAsync(TickerFunctionContext context, CancellationToken ct) =>
        _coordinator.ExecuteIfLeaderAsync(
            TickerQFunctionNames.Reindex,
            async token =>
            {
                _logger.LogInformation("Executing reindex recurring job for ticker {TickerId}.", context.Id);
                await _reindexService.ReindexFullTextSearchAsync(token);
            },
            ct
        );
}
