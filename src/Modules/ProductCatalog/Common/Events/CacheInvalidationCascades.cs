using SharedKernel.Contracts.Events;

namespace ProductCatalog.Common.Events;

public static class CacheInvalidationCascades
{
    public static readonly IReadOnlyList<CacheInvalidationNotification> ForProductChange =
    [
        new(CacheTags.Products),
        new(CacheTags.Categories),
    ];

    public static readonly IReadOnlyList<CacheInvalidationNotification> ForProductDeletion =
    [
        new(CacheTags.Products),
        new(CacheTags.Categories),
        new(CrossModuleCacheTags.Reviews),
    ];

    public static readonly IReadOnlyList<CacheInvalidationNotification> ForCategoryDeletion =
    [
        new(CacheTags.Categories),
        new(CacheTags.Products),
    ];

    public static readonly IReadOnlyList<CacheInvalidationNotification> ForProductDataDeletion =
    [
        new(CacheTags.ProductData),
        new(CacheTags.Products),
    ];
}
