using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using ProductCatalog.Logging;
using ProductCatalog.Persistence;
using SharedKernel.Contracts.Commands.Cleanup;

namespace ProductCatalog.Handlers;

/// <summary>
///     Wolverine handler that processes <see cref="CleanupOrphanedProductDataCommand" /> dispatched by the
///     BackgroundJobs module. Uses a two-phase mark-and-sweep approach to safely identify and delete
///     MongoDB ProductData documents that have no corresponding ProductDataLink in PostgreSQL.
///     <para>
///         <b>Phase 1 (Mark):</b> Identifies orphan candidates and sets <c>PendingDeletion = true</c>.
///         Does NOT delete — gives concurrent link-creation operations time to complete.
///     </para>
///     <para>
///         <b>Phase 2 (Sweep):</b> Deletes documents that were marked on a <em>previous</em> run and
///         still have no links. Clears <c>PendingDeletion</c> on documents that acquired links since marking.
///     </para>
/// </summary>
public sealed class CleanupOrphanedProductDataHandler
{
    public static async Task HandleAsync(
        CleanupOrphanedProductDataCommand command,
        ProductCatalogDbContext dbContext,
        MongoDbContext mongoDbContext,
        TimeProvider timeProvider,
        ILogger<CleanupOrphanedProductDataHandler> logger,
        CancellationToken ct
    )
    {
        IMongoCollection<ProductData> mongoCollection = mongoDbContext.ProductData;
        DateTime cutoff = timeProvider.GetUtcNow().UtcDateTime.AddDays(-command.RetentionDays);
        int totalMarked = 0;
        int totalDeleted = 0;
        int totalCleared = 0;
        Guid? lastSeenId = null;

        // ── Phase 1: Mark orphan candidates ──────────────────────────────────────
        while (true)
        {
            FilterDefinition<ProductData> pageFilter =
                Builders<ProductData>.Filter.Lt(d => d.CreatedAt, cutoff)
                & Builders<ProductData>.Filter.Eq(d => d.PendingDeletion, false)
                & Builders<ProductData>.Filter.Eq(d => d.IsDeleted, false);

            if (lastSeenId.HasValue)
                pageFilter &= Builders<ProductData>.Filter.Gt(d => d.Id, lastSeenId.Value);

            List<Guid> page = await mongoCollection
                .Find(pageFilter)
                .SortBy(d => d.Id)
                .Limit(command.BatchSize)
                .Project(d => d.Id)
                .ToListAsync(ct);

            if (page.Count == 0)
                break;

            List<Guid> linkedIds = await dbContext
                .ProductDataLinks.Where(l => page.Contains(l.ProductDataId))
                .Select(l => l.ProductDataId)
                .Distinct()
                .ToListAsync(ct);

            HashSet<Guid> linkedIdSet = linkedIds.ToHashSet();
            Guid[] orphanCandidates = page.Where(id => !linkedIdSet.Contains(id)).ToArray();

            if (orphanCandidates.Length > 0)
            {
                await mongoCollection.UpdateManyAsync(
                    Builders<ProductData>.Filter.In(d => d.Id, orphanCandidates),
                    Builders<ProductData>.Update.Set(d => d.PendingDeletion, true),
                    cancellationToken: ct
                );
                totalMarked += orphanCandidates.Length;
            }

            lastSeenId = page[^1];
        }

        // ── Phase 2: Sweep previously-marked documents ───────────────────────────
        List<Guid> pendingIds = await mongoCollection
            .Find(Builders<ProductData>.Filter.Eq(d => d.PendingDeletion, true))
            .Project(d => d.Id)
            .ToListAsync(ct);

        if (pendingIds.Count > 0)
        {
            List<Guid> stillLinked = await dbContext
                .ProductDataLinks.Where(l => pendingIds.Contains(l.ProductDataId))
                .Select(l => l.ProductDataId)
                .Distinct()
                .ToListAsync(ct);

            HashSet<Guid> stillLinkedSet = stillLinked.ToHashSet();
            Guid[] safeToDelete = pendingIds.Where(id => !stillLinkedSet.Contains(id)).ToArray();
            Guid[] falsePending = pendingIds.Where(id => stillLinkedSet.Contains(id)).ToArray();

            if (safeToDelete.Length > 0)
            {
                await mongoCollection.DeleteManyAsync(
                    Builders<ProductData>.Filter.In(d => d.Id, safeToDelete),
                    ct
                );
                totalDeleted += safeToDelete.Length;
            }

            if (falsePending.Length > 0)
            {
                await mongoCollection.UpdateManyAsync(
                    Builders<ProductData>.Filter.In(d => d.Id, falsePending),
                    Builders<ProductData>.Update.Set(d => d.PendingDeletion, false),
                    cancellationToken: ct
                );
                totalCleared += falsePending.Length;
            }
        }

        if (totalMarked > 0)
            logger.OrphanedProductDataMarked(totalMarked);

        if (totalDeleted > 0 || totalCleared > 0)
            logger.OrphanedProductDataSwept(totalDeleted, totalCleared);
    }
}
