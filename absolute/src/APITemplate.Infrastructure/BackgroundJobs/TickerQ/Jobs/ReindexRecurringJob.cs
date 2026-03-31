using APITemplate.Application.Common.BackgroundJobs;
using APITemplate.Infrastructure.BackgroundJobs.TickerQ.Coordination;
using Microsoft.Extensions.Logging;
using TickerQ.Utilities.Base;

namespace APITemplate.Infrastructure.BackgroundJobs.TickerQ.Jobs;

/// <summary>
/// TickerQ recurring job that triggers a full-text search index rebuild through <see cref="IReindexService"/>.
/// Execution is gated by <see cref="IDistributedJobCoordinator"/> to prevent multi-node duplication.
/// </summary>
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

    /// <summary>TickerQ entry-point that acquires the distributed leader lease and invokes the reindex service.</summary>
    [TickerFunction(TickerQFunctionNames.Reindex)]
    public Task ExecuteAsync(TickerFunctionContext context, CancellationToken ct) =>
        _coordinator.ExecuteIfLeaderAsync(
            TickerQFunctionNames.Reindex,
            async token =>
            {
                _logger.LogInformation(
                    "Executing reindex recurring job for ticker {TickerId}.",
                    context.Id
                );
                await _reindexService.ReindexFullTextSearchAsync(token);
            },
            ct
        );
}
