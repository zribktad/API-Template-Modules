using SharedKernel.Contracts.Events;

namespace APITemplate.Api.Cache;

public sealed class CacheInvalidationHandler
{
    public static Task HandleAsync(
        CacheInvalidationNotification @event,
        IOutputCacheInvalidationService outputCacheInvalidationService,
        CancellationToken ct
    ) => outputCacheInvalidationService.EvictAsync(@event.CacheTag, ct);
}
