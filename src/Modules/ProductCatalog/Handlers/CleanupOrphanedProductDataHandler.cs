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

            HashSet<Guid> linkedIdSet = await GetLinkedIdsAsync(dbContext, page, ct);
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
        lastSeenId = null;

        while (true)
        {
            FilterDefinition<ProductData> pendingFilter = Builders<ProductData>.Filter.Eq(
                d => d.PendingDeletion,
                true
            );

            if (lastSeenId.HasValue)
                pendingFilter &= Builders<ProductData>.Filter.Gt(d => d.Id, lastSeenId.Value);

            List<Guid> pendingPage = await mongoCollection
                .Find(pendingFilter)
                .SortBy(d => d.Id)
                .Limit(command.BatchSize)
                .Project(d => d.Id)
                .ToListAsync(ct);

            if (pendingPage.Count == 0)
                break;

            HashSet<Guid> stillLinkedSet = await GetLinkedIdsAsync(dbContext, pendingPage, ct);
            List<Guid> safeToDelete = new();
            List<Guid> falsePending = new();
            foreach (Guid id in pendingPage)
            {
                if (stillLinkedSet.Contains(id))
                    falsePending.Add(id);
                else
                    safeToDelete.Add(id);
            }

            if (safeToDelete.Count > 0)
            {
                await mongoCollection.DeleteManyAsync(
                    Builders<ProductData>.Filter.In(d => d.Id, safeToDelete),
                    ct
                );
                totalDeleted += safeToDelete.Count;
            }

            if (falsePending.Count > 0)
            {
                await mongoCollection.UpdateManyAsync(
                    Builders<ProductData>.Filter.In(d => d.Id, falsePending),
                    Builders<ProductData>.Update.Set(d => d.PendingDeletion, false),
                    cancellationToken: ct
                );
                totalCleared += falsePending.Count;
            }

            lastSeenId = pendingPage[^1];
        }

        if (totalMarked > 0)
            logger.OrphanedProductDataMarked(totalMarked);

        if (totalDeleted > 0 || totalCleared > 0)
            logger.OrphanedProductDataSwept(totalDeleted, totalCleared);
    }

    private static async Task<HashSet<Guid>> GetLinkedIdsAsync(
        ProductCatalogDbContext dbContext,
        List<Guid> candidateIds,
        CancellationToken ct
    )
    {
        List<Guid> linkedIds = await dbContext
            .ProductDataLinks.IgnoreQueryFilters() // Bypass tenant filter since this is a global background job
            .Where(l => candidateIds.Contains(l.ProductDataId))
            .Select(l => l.ProductDataId)
            .Distinct()
            .ToListAsync(ct);

        return linkedIds.ToHashSet();
    }
}
