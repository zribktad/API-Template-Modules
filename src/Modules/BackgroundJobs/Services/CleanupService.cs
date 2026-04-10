using BackgroundJobs.Logging;
using Microsoft.Extensions.Logging;
using SharedKernel.Contracts.Commands.Cleanup;
using Wolverine;

namespace BackgroundJobs.Services;

/// <summary>
///     Infrastructure implementation of <see cref="ICleanupService" /> that orchestrates
///     scheduled data-hygiene tasks. Cross-module operations (invitations, orphaned product data)
///     are delegated to owning modules via bus commands. Soft-delete cleanup is local.
/// </summary>
public sealed class CleanupService : ICleanupService
{
    private readonly IEnumerable<ISoftDeleteCleanupStrategy> _cleanupStrategies;
    private readonly ILogger<CleanupService> _logger;
    private readonly IMessageBus _messageBus;
    private readonly TimeProvider _timeProvider;

    public CleanupService(
        IMessageBus messageBus,
        IEnumerable<ISoftDeleteCleanupStrategy> cleanupStrategies,
        TimeProvider timeProvider,
        ILogger<CleanupService> logger
    )
    {
        _messageBus = messageBus;
        _cleanupStrategies = cleanupStrategies;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task CleanupExpiredInvitationsAsync(
        int retentionHours,
        int batchSize,
        CancellationToken ct = default
    )
    {
        await _messageBus.InvokeAsync(
            new CleanupExpiredInvitationsCommand(retentionHours, batchSize),
            ct
        );
    }

    public async Task CleanupSoftDeletedRecordsAsync(
        int retentionDays,
        int batchSize,
        CancellationToken ct = default
    )
    {
        DateTime cutoff = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-retentionDays);

        foreach (ISoftDeleteCleanupStrategy strategy in _cleanupStrategies)
        {
            int deleted = await strategy.CleanupAsync(cutoff, batchSize, ct);

            if (deleted > 0)
                _logger.CleanedUpSoftDeletedRecords(deleted, strategy.EntityName);
        }
    }

    public async Task CleanupOrphanedProductDataAsync(
        int retentionDays,
        int batchSize,
        CancellationToken ct = default
    )
    {
        await _messageBus.InvokeAsync(
            new CleanupOrphanedProductDataCommand(retentionDays, batchSize),
            ct
        );
    }

    public async Task CleanupExpiredBffSessionsAsync(int batchSize, CancellationToken ct = default)
    {
        await _messageBus.InvokeAsync(new CleanupExpiredBffSessionsCommand(batchSize), ct);
    }
}
