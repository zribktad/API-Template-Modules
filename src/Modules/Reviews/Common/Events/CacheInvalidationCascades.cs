using SharedKernel.Contracts.Events;

namespace Reviews.Common.Events;

public static class CacheInvalidationCascades
{
    public static IReadOnlyList<CacheInvalidationNotification> ForReviewChange(Guid tenantId) =>
        [new(CacheTags.Reviews, tenantId), new(CrossModuleCacheTags.Categories, tenantId)];
}
