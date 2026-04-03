using BackgroundJobs.Shared;
using BackgroundJobs.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel.Application.BackgroundJobs;
using SharedKernel.Application.Options.BackgroundJobs;
using TickerQ.Utilities.Base;

namespace BackgroundJobs.TickerQ.Jobs;

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

    [TickerFunction(TickerQFunctionNames.Cleanup)]
    public Task ExecuteAsync(TickerFunctionContext context, CancellationToken ct) =>
        _coordinator.ExecuteIfLeaderAsync(
            TickerQFunctionNames.Cleanup,
            async token =>
            {
                _logger.ExecutingCleanupRecurringJob(context.Id);

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


