using ProductCatalog.Application.Features.Tenant.Specifications;
using ProductCatalog.Domain;
using Wolverine;
using CategoryEntity = ProductCatalog.Domain.Entities.Category;
using ProductEntity = ProductCatalog.Domain.Entities.Product;

namespace ProductCatalog.Application.Features.Tenant.Handlers;

/// <summary>
/// Handles <see cref="TenantSoftDeletedNotification"/> by cascading the soft-delete to all
/// non-deleted <see cref="CategoryEntity"/> and <see cref="ProductEntity"/> records for the tenant.
/// Publishes <see cref="ProductSoftDeletedNotification"/> per product so the Reviews module
/// can cascade to ProductReviews, and invalidates the Products and Categories cache.
/// </summary>
public static class TenantCascadeDeleteHandler
{
    public static async Task<OutgoingMessages> Handle(
        TenantSoftDeletedNotification notification,
        ICategoryRepository categoryRepository,
        IProductRepository productRepository,
        IUnitOfWork<ProductCatalogDbMarker> unitOfWork,
        CancellationToken ct
    )
    {
        IReadOnlyList<CategoryEntity> categories = await categoryRepository.ListAsync(
            new CategoriesForTenantSoftDeleteSpecification(notification.TenantId),
            ct
        );

        IReadOnlyList<ProductEntity> products = await productRepository.ListAsync(
            new ProductsForTenantSoftDeleteSpecification(notification.TenantId),
            ct
        );

        if (categories.Count == 0 && products.Count == 0)
            return OutgoingMessagesHelper.Empty;

        await unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                if (categories.Count > 0)
                    await categoryRepository.DeleteRangeAsync(categories, ct);

                if (products.Count > 0)
                    await productRepository.DeleteRangeAsync(products, ct);
            },
            ct
        );

        OutgoingMessages messages = new();
        messages.Add(new CacheInvalidationNotification(CacheTags.Products));
        messages.Add(new CacheInvalidationNotification(CacheTags.Categories));

        foreach (ProductEntity product in products)
        {
            messages.Add(
                new ProductSoftDeletedNotification(
                    product.Id,
                    notification.ActorId,
                    notification.DeletedAtUtc
                )
            );
        }

        return messages;
    }
}
