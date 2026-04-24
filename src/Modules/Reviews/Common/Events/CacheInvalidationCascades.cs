namespace Reviews.Common.Events;

public static class CacheInvalidationCascades
{
    public static IEnumerable<CacheInvalidationNotification> ForReviewChange() =>
        [new(CacheTags.Reviews), new(CacheTags.Categories)];
}
