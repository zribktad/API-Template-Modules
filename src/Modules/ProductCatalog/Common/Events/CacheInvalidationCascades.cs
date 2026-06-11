using SharedKernel.Contracts.Events;

namespace ProductCatalog.Common.Events;

public static class CacheInvalidationCascades
{
    public static IReadOnlyList<CacheInvalidationNotification> ForProductChange(Guid tenantId) =>
        [new(CacheTags.Products, tenantId), new(CacheTags.Categories, tenantId)];

    public static IReadOnlyList<CacheInvalidationNotification> ForProductDeletion(Guid tenantId) =>
        [
            new(CacheTags.Products, tenantId),
            new(CacheTags.Categories, tenantId),
            new(CrossModuleCacheTags.Reviews, tenantId),
        ];

    public static IReadOnlyList<CacheInvalidationNotification> ForCategoryDeletion(Guid tenantId) =>
        [new(CacheTags.Categories, tenantId), new(CacheTags.Products, tenantId)];

    public static IReadOnlyList<CacheInvalidationNotification> ForProductDataDeletion(
        Guid tenantId
    ) => [new(CacheTags.ProductData, tenantId), new(CacheTags.Products, tenantId)];
}
