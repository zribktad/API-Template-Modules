namespace SharedKernel.Contracts.Events;

/// <summary>
///     Cache invalidation event. Use with a CacheTags constant
///     to signal that a specific cache region must be evicted.
/// </summary>
public sealed record CacheInvalidationNotification(string CacheTag);
