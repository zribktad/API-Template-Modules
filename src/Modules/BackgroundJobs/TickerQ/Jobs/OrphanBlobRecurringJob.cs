using BackgroundJobs.Contracts;
using Microsoft.Extensions.Logging;
using SharedKernel.Application.BackgroundJobs;
using TickerQ.Utilities.Base;

namespace BackgroundJobs.TickerQ.Jobs;

/// <summary>
///     Recurring job that triggers the FileStorage orphan-blob sweep via a durable bus command. Leader-gated
///     so only one node runs the sweep in a given interval.
/// </summary>
public sealed class OrphanBlobRecurringJob
{
    private readonly IDistributedJobCoordinator _coordinator;
    private readonly IOrphanBlobJobService _orphanBlobJobService;
    private readonly ILogger<OrphanBlobRecurringJob> _logger;

    public OrphanBlobRecurringJob(
        IOrphanBlobJobService orphanBlobJobService,
        IDistributedJobCoordinator coordinator,
        ILogger<OrphanBlobRecurringJob> logger
    )
    {
        _orphanBlobJobService = orphanBlobJobService;
        _coordinator = coordinator;
        _logger = logger;
    }

    [TickerFunction("orphan-blob-recurring-job")]
    public Task ExecuteAsync(TickerFunctionContext context, CancellationToken ct)
    {
        return _coordinator.ExecuteIfLeaderAsync(
            "orphan-blob-recurring-job",
            async token =>
            {
                _logger.LogInformation(
                    "Executing orphan-blob recurring job (tickerId={TickerId})",
                    context.Id
                );
                await _orphanBlobJobService.RunSweepAsync(token);
            },
            ct
        );
    }
}
