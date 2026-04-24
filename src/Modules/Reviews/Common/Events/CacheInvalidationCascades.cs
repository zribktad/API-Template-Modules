namespace Reviews.Common.Events;

public static class CacheInvalidationCascades
{
    public static readonly IReadOnlyList<CacheInvalidationNotification> ForReviewChange =
    [
        new(CacheTags.Reviews),
        new(CacheTags.Categories),
    ];
}
