namespace APITemplate.Application.Common.Events;

/// <summary>
/// Cache invalidation event. Use with a <see cref="CacheTags"/> constant
/// to signal that a specific cache region must be evicted.
/// </summary>
public sealed record CacheInvalidationNotification(string CacheTag);
