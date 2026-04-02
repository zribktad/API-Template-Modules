using APITemplate.Application.Common.BackgroundJobs;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Enums;
using APITemplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace APITemplate.Infrastructure.BackgroundJobs.Services;

/// <summary>
/// Infrastructure implementation of <see cref="ICleanupService"/> that performs
/// scheduled data-hygiene tasks: expired invitations, soft-deleted records, and orphaned MongoDB documents.
/// </summary>
public sealed class CleanupService : ICleanupService
{
    private readonly AppDbContext _dbContext;
    private readonly MongoDbContext? _mongoDbContext;
    private readonly IEnumerable<ISoftDeleteCleanupStrategy> _cleanupStrategies;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CleanupService> _logger;

    public CleanupService(
        AppDbContext dbContext,
        IEnumerable<ISoftDeleteCleanupStrategy> cleanupStrategies,
        TimeProvider timeProvider,
        ILogger<CleanupService> logger,
        MongoDbContext? mongoDbContext = null
    )
    {
        _dbContext = dbContext;
        _cleanupStrategies = cleanupStrategies;
        _timeProvider = timeProvider;
        _mongoDbContext = mongoDbContext;
        _logger = logger;
    }

    /// <summary>
    /// Permanently deletes pending tenant invitations whose expiry timestamp is older than
    /// <paramref name="retentionHours"/> hours, processed in batches of <paramref name="batchSize"/>.
    /// </summary>
    public async Task CleanupExpiredInvitationsAsync(
        int retentionHours,
        int batchSize,
        CancellationToken ct = default
    )
    {
        var cutoff = _timeProvider.GetUtcNow().UtcDateTime.AddHours(-retentionHours);
        int totalDeleted = 0;
        int deleted;

        do
        {
            deleted = await _dbContext
                .TenantInvitations.IgnoreQueryFilters()
                .Where(i => i.Status == InvitationStatus.Pending && i.ExpiresAtUtc < cutoff)
                .OrderBy(i => i.ExpiresAtUtc)
                .Take(batchSize)
                .ExecuteDeleteAsync(ct);

            totalDeleted += deleted;
        } while (deleted == batchSize);

        if (totalDeleted > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired invitations.", totalDeleted);
        }
    }

    /// <summary>
    /// Delegates to every registered <see cref="ISoftDeleteCleanupStrategy"/> to hard-delete
    /// soft-deleted records older than <paramref name="retentionDays"/> days.
    /// </summary>
    public async Task CleanupSoftDeletedRecordsAsync(
        int retentionDays,
        int batchSize,
        CancellationToken ct = default
    )
    {
        var cutoff = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-retentionDays);

        foreach (var strategy in _cleanupStrategies)
        {
            var deleted = await strategy.CleanupAsync(cutoff, batchSize, ct);

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

    /// <summary>
    /// Safety net for orphaned MongoDB ProductData documents that are no longer linked
    /// from any ProductDataLink in PostgreSQL. Under normal operation, cascade rules
    /// (ProductSoftDeleteCascadeRule, ProductDataCascadeDeleteHandler) handle cleanup.
    /// Orphans may appear after transaction failures, manual DB edits, or cascade bugs.
    /// </summary>
    public async Task CleanupOrphanedProductDataAsync(
        int retentionDays,
        int batchSize,
        CancellationToken ct = default
    )
    {
        if (_mongoDbContext is null)
        {
            _logger.LogDebug(
                "MongoDbContext not available, skipping orphaned product data cleanup."
            );
            return;
        }

        var mongoCollection = _mongoDbContext.ProductData;
        var cutoff = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-retentionDays);
        var totalDeleted = 0;
        Guid? lastSeenId = null;

        while (true)
        {
            var pageFilter = Builders<ProductData>.Filter.Lt(d => d.CreatedAt, cutoff);
            if (lastSeenId.HasValue)
            {
                pageFilter &= Builders<ProductData>.Filter.Gt(d => d.Id, lastSeenId.Value);
            }

            var page = await mongoCollection
                .Find(pageFilter)
                .SortBy(d => d.Id)
                .Limit(batchSize)
                .Project(d => d.Id)
                .ToListAsync(ct);

            if (page.Count == 0)
            {
                break;
            }

            var linkedIds = await _dbContext
                .ProductDataLinks.IgnoreQueryFilters()
                .Where(l => page.Contains(l.ProductDataId))
                .Select(l => l.ProductDataId)
                .Distinct()
                .ToListAsync(ct);

            var linkedIdSet = linkedIds.ToHashSet();
            var orphanedIds = page.Where(id => !linkedIdSet.Contains(id)).ToArray();

            if (orphanedIds.Length > 0)
            {
                var deleteFilter = Builders<ProductData>.Filter.In(d => d.Id, orphanedIds);
                await mongoCollection.DeleteManyAsync(deleteFilter, ct);
                totalDeleted += orphanedIds.Length;
            }

            lastSeenId = page[^1];
        }

        if (totalDeleted > 0)
        {
            _logger.LogInformation(
                "Cleaned up {Count} orphaned product data documents.",
                totalDeleted
            );
        }
    }
}
