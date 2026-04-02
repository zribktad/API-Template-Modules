using Contracts.Commands.Cleanup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using ProductCatalog.Infrastructure.Persistence;

namespace ProductCatalog.Infrastructure.Handlers;

/// <summary>
/// Wolverine handler that processes <see cref="CleanupOrphanedProductDataCommand"/> dispatched by the
/// BackgroundJobs module. Identifies MongoDB ProductData documents that have no corresponding
/// ProductDataLink in PostgreSQL and deletes them in paginated batches.
/// </summary>
public sealed class CleanupOrphanedProductDataHandler
{
    public static async Task HandleAsync(
        CleanupOrphanedProductDataCommand command,
        ProductCatalogDbContext dbContext,
        MongoDbContext mongoDbContext,
        TimeProvider timeProvider,
        ILogger<CleanupOrphanedProductDataHandler> logger,
        CancellationToken ct)
    {
        IMongoCollection<ProductData> mongoCollection = mongoDbContext.ProductData;
        DateTime cutoff = timeProvider.GetUtcNow().UtcDateTime.AddDays(-command.RetentionDays);
        int totalDeleted = 0;
        Guid? lastSeenId = null;

        while (true)
        {
            FilterDefinition<ProductData> pageFilter =
                Builders<ProductData>.Filter.Lt(d => d.CreatedAt, cutoff);

            if (lastSeenId.HasValue)
            {
                pageFilter &= Builders<ProductData>.Filter.Gt(d => d.Id, lastSeenId.Value);
            }

            List<Guid> page = await mongoCollection
                .Find(pageFilter)
                .SortBy(d => d.Id)
                .Limit(command.BatchSize)
                .Project(d => d.Id)
                .ToListAsync(ct);

            if (page.Count == 0)
            {
                break;
            }

            List<Guid> linkedIds = await dbContext
                .ProductDataLinks
                .IgnoreQueryFilters()
                .Where(l => page.Contains(l.ProductDataId))
                .Select(l => l.ProductDataId)
                .Distinct()
                .ToListAsync(ct);

            HashSet<Guid> linkedIdSet = linkedIds.ToHashSet();
            Guid[] orphanedIds = page.Where(id => !linkedIdSet.Contains(id)).ToArray();

            if (orphanedIds.Length > 0)
            {
                FilterDefinition<ProductData> deleteFilter =
                    Builders<ProductData>.Filter.In(d => d.Id, orphanedIds);
                await mongoCollection.DeleteManyAsync(deleteFilter, ct);
                totalDeleted += orphanedIds.Length;
            }

            lastSeenId = page[^1];
        }

        if (totalDeleted > 0)
        {
            logger.LogInformation(
                "Cleaned up {Count} orphaned product data documents.", totalDeleted);
        }
    }
}
