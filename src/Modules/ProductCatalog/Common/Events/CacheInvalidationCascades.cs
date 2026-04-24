namespace ProductCatalog.Common.Events;

public static class CacheInvalidationCascades
{
    public static IEnumerable<CacheInvalidationNotification> ForProductChange() =>
        [new(CacheTags.Products), new(CacheTags.Categories)];

    public static IEnumerable<CacheInvalidationNotification> ForProductDeletion() =>
        [new(CacheTags.Products), new(CacheTags.Categories), new(CacheTags.Reviews)];

    public static IEnumerable<CacheInvalidationNotification> ForCategoryDeletion() =>
        [new(CacheTags.Categories), new(CacheTags.Products)];

    public static IEnumerable<CacheInvalidationNotification> ForProductDataDeletion() =>
        [new(CacheTags.ProductData), new(CacheTags.Products)];
}
