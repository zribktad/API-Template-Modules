using APITemplate.Application.Common.BackgroundJobs;
using APITemplate.Application.Common.Options;
using APITemplate.Infrastructure.BackgroundJobs.TickerQ.Coordination;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TickerQ.Utilities.Base;

namespace APITemplate.Infrastructure.BackgroundJobs.TickerQ.Jobs;

/// <summary>
/// TickerQ recurring job that orchestrates all data-hygiene cleanup tasks (expired invitations,
/// soft-deleted records, orphaned MongoDB documents) through <see cref="ICleanupService"/>.
/// Execution is gated by <see cref="IDistributedJobCoordinator"/> to prevent multi-node duplication.
/// </summary>
public sealed class CleanupRecurringJob
{
    private readonly ICleanupService _cleanupService;
    private readonly IDistributedJobCoordinator _coordinator;
    private readonly CleanupJobOptions _options;
    private readonly ILogger<CleanupRecurringJob> _logger;

    public CleanupRecurringJob(
        ICleanupService cleanupService,
        IDistributedJobCoordinator coordinator,
        IOptions<BackgroundJobsOptions> options,
        ILogger<CleanupRecurringJob> logger
    )
    {
        _cleanupService = cleanupService;
        _coordinator = coordinator;
        _options = options.Value.Cleanup;
        _logger = logger;
    }

    /// <summary>
    /// TickerQ entry-point that acquires the distributed leader lease and sequentially runs
    /// all three cleanup operations defined in <see cref="CleanupJobOptions"/>.
    /// </summary>
    [TickerFunction(TickerQFunctionNames.Cleanup)]
    public Task ExecuteAsync(TickerFunctionContext context, CancellationToken ct) =>
        _coordinator.ExecuteIfLeaderAsync(
            TickerQFunctionNames.Cleanup,
            async token =>
            {
                _logger.LogInformation(
                    "Executing cleanup recurring job for ticker {TickerId}.",
                    context.Id
                );

                await _cleanupService.CleanupExpiredInvitationsAsync(
                    _options.ExpiredInvitationRetentionHours,
                    _options.BatchSize,
                    token
                );
                await _cleanupService.CleanupSoftDeletedRecordsAsync(
                    _options.SoftDeleteRetentionDays,
                    _options.BatchSize,
                    token
                );
                await _cleanupService.CleanupOrphanedProductDataAsync(
                    _options.OrphanedProductDataRetentionDays,
                    _options.BatchSize,
                    token
                );
            },
            ct
        );
}
