using Wolverine;

namespace ProductCatalog.Features.TenantCascadeDelete;

/// <summary>
///     Handles <see cref="TenantSoftDeletedNotification" /> by cascading the soft-delete to all
///     non-deleted categories, product data links, and products for the tenant using bulk SQL
///     (<c>ExecuteUpdateAsync</c>) — zero entity materialization.
///     Publishes a single <see cref="ProductsBatchSoftDeletedNotification" /> so the Reviews module
///     can cascade soft-delete to ProductReviews in one batch, and invalidates the Products, Categories,
///     and Reviews cache.
/// </summary>
public static class TenantCascadeDeleteHandler
{
    public static async Task<OutgoingMessages> Handle(
        TenantSoftDeletedNotification notification,
        ICategoryRepository categoryRepository,
        IProductRepository productRepository,
        IProductDataLinkRepository linkRepository,
        IUnitOfWork<ProductCatalogDbMarker> unitOfWork,
        IIdGenerator idGenerator,
        CancellationToken ct
    )
    {
        IReadOnlyList<Guid> productIds = await productRepository.GetNonDeletedIdsByTenantAsync(
            notification.TenantId,
            ct
        );

        await unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                await categoryRepository.BulkSoftDeleteByTenantAsync(
                    notification.TenantId,
                    notification.ActorId,
                    notification.DeletedAtUtc,
                    ct
                );

                await linkRepository.BulkSoftDeleteByTenantAsync(
                    notification.TenantId,
                    notification.ActorId,
                    notification.DeletedAtUtc,
                    ct
                );

                await productRepository.BulkSoftDeleteByTenantAsync(
                    notification.TenantId,
                    notification.ActorId,
                    notification.DeletedAtUtc,
                    ct
                );
            },
            ct
        );

        OutgoingMessages messages = new();
        messages.AddRange(CacheInvalidationCascades.ForProductDeletion);

        if (productIds.Count > 0)
        {
            messages.Add(
                new ProductsBatchSoftDeletedNotification(
                    productIds,
                    notification.TenantId,
                    notification.ActorId,
                    notification.DeletedAtUtc,
                    idGenerator.NewId()
                )
            );
        }

        return messages;
    }
}
