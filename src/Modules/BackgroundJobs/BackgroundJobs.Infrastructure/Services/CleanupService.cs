using BackgroundJobs.Application.Services;
using Contracts.Commands.Cleanup;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace BackgroundJobs.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="ICleanupService"/> that orchestrates
/// scheduled data-hygiene tasks. Cross-module operations (invitations, orphaned product data)
/// are delegated to owning modules via bus commands. Soft-delete cleanup is local.
/// </summary>
public sealed class CleanupService : ICleanupService
{
    private readonly IMessageBus _messageBus;
    private readonly IEnumerable<ISoftDeleteCleanupStrategy> _cleanupStrategies;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CleanupService> _logger;

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
            {
                _logger.LogInformation(
                    "Cleaned up {Count} soft-deleted records from {Entity}.",
                    deleted,
                    strategy.EntityName
                );
            }
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
}
