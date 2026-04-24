using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ProductCatalog.Entities;
using SharedKernel.Application.Context;

namespace ProductCatalog.Persistence.Interceptors;

/// <summary>
///     Cascades a tracked <see cref="Product" /> soft-delete to its <see cref="ProductDataLink" /> rows
///     within the same <c>SaveChangesAsync</c> transaction via a single bulk <c>ExecuteUpdateAsync</c>
///     (zero entity materialization of links).
///     Does NOT cover the <c>ExecuteUpdateAsync</c>-based tenant-cascade path — that bypasses ChangeTracker
///     and must still call <c>BulkSoftDeleteByTenantAsync</c> on links explicitly.
/// </summary>
public sealed class ProductLinkSoftDeleteCascadeInterceptor(
    IActorProvider actorProvider,
    TimeProvider timeProvider
) : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        if (eventData.Context is not ProductCatalogDbContext context)
            return result;

        List<Guid> softDeletedProductIds = CollectFreshlySoftDeletedProductIds(context);
        if (softDeletedProductIds.Count == 0)
            return result;

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        Guid actor = actorProvider.ActorId;

        await context
            .ProductDataLinks.IgnoreQueryFilters()
            .Where(link => softDeletedProductIds.Contains(link.ProductId) && !link.IsDeleted)
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(l => l.IsDeleted, true)
                        .SetProperty(l => l.DeletedAtUtc, now)
                        .SetProperty(l => l.DeletedBy, (Guid?)actor)
                        .SetProperty(l => l.Audit.UpdatedAtUtc, (DateTime?)now)
                        .SetProperty(l => l.Audit.UpdatedBy, (Guid?)actor),
                cancellationToken
            );

        return result;
    }

    private static List<Guid> CollectFreshlySoftDeletedProductIds(ProductCatalogDbContext context)
    {
        List<Guid> ids = [];
        foreach (EntityEntry<Product> entry in context.ChangeTracker.Entries<Product>())
        {
            if (entry.State != EntityState.Modified)
                continue;

            PropertyEntry isDeletedProp = entry.Property(nameof(Product.IsDeleted));
            if (!isDeletedProp.IsModified)
                continue;

            if (isDeletedProp.CurrentValue is not true)
                continue;

            ids.Add(entry.Entity.Id);
        }
        return ids;
    }
}
