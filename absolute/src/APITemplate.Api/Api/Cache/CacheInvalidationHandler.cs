using APITemplate.Application.Common.Events;

namespace APITemplate.Api.Cache;

/// <summary>
/// Handles <see cref="CacheInvalidationNotification"/> by evicting tagged output cache entries.
/// </summary>
public sealed class CacheInvalidationHandler
{
    public static Task HandleAsync(
        CacheInvalidationNotification @event,
        IOutputCacheInvalidationService outputCacheInvalidationService,
        CancellationToken ct
    ) => outputCacheInvalidationService.EvictAsync(@event.CacheTag, ct);
}
