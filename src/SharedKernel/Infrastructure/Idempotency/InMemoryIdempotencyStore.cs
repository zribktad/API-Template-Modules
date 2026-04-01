using System.Collections.Concurrent;
using System.Text.Json;
using SharedKernel.Application.Contracts;

namespace SharedKernel.Infrastructure.Idempotency;

/// <summary>
/// Single-process, in-memory implementation of <see cref="IIdempotencyStore"/> backed by
/// <see cref="ConcurrentDictionary{TKey,TValue}"/>. Suitable for development and single-instance
/// deployments; TTL enforcement is done lazily on access via <c>EvictExpired</c>.
/// </summary>
public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private static readonly TimeSpan EvictionInterval = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<string, (string Value, DateTimeOffset Expiry)> _store =
        new();
    private readonly TimeProvider _timeProvider;
    private DateTimeOffset _lastEviction = DateTimeOffset.MinValue;

    public InMemoryIdempotencyStore(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <summary>Returns the cached entry for <paramref name="key"/> if it exists and has not expired; triggers lazy eviction otherwise.</summary>
    public Task<IdempotencyCacheEntry?> TryGetAsync(string key, CancellationToken ct = default)
    {
        if (
            _store.TryGetValue(key, out (string Value, DateTimeOffset Expiry) entry)
            && entry.Expiry > _timeProvider.GetUtcNow()
        )
        {
            IdempotencyCacheEntry? result = JsonSerializer.Deserialize<IdempotencyCacheEntry>(
                entry.Value
            );
            return Task.FromResult(result);
        }

        EvictExpired();
        return Task.FromResult<IdempotencyCacheEntry?>(null);
    }

    /// <summary>Attempts to insert a lock entry using <c>TryAdd</c>; returns the lock token if acquired, or <c>null</c> otherwise. Atomically checks that no cached result already exists for the key.</summary>
    public Task<string?> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken ct = default)
    {
        EvictExpired();

        DateTimeOffset now = _timeProvider.GetUtcNow();

        if (
            _store.TryGetValue(key, out (string Value, DateTimeOffset Expiry) existing)
            && existing.Expiry > now
        )
        {
            return Task.FromResult<string?>(null);
        }

        string lockKey = key + IdempotencyStoreConstants.LockSuffix;
        string lockValue = Guid.NewGuid().ToString("N");
        DateTimeOffset expiry = now.Add(ttl);
        bool acquired = _store.TryAdd(lockKey, (lockValue, expiry));

        return Task.FromResult(acquired ? lockValue : null);
    }

    /// <summary>Serialises <paramref name="entry"/> and inserts or replaces it in the store with the specified <paramref name="ttl"/>.</summary>
    public Task SetAsync(
        string key,
        IdempotencyCacheEntry entry,
        TimeSpan ttl,
        CancellationToken ct = default
    )
    {
        string json = JsonSerializer.Serialize(entry);
        DateTimeOffset expiry = _timeProvider.GetUtcNow().Add(ttl);
        _store[key] = (json, expiry);
        return Task.CompletedTask;
    }

    /// <summary>Removes the lock entry for <paramref name="key"/> only if the supplied <paramref name="lockToken"/> still matches, preventing accidental release of expired locks.</summary>
    public Task ReleaseAsync(string key, string lockToken, CancellationToken ct = default)
    {
        string lockKey = key + IdempotencyStoreConstants.LockSuffix;

        if (
            _store.TryGetValue(lockKey, out (string Value, DateTimeOffset Expiry) existing)
            && existing.Value == lockToken
        )
        {
            _store.TryRemove(
                new KeyValuePair<string, (string Value, DateTimeOffset Expiry)>(lockKey, existing)
            );
        }

        return Task.CompletedTask;
    }

    /// <summary>Lazily removes all entries whose expiry has passed, keeping memory usage bounded without a dedicated timer.</summary>
    private void EvictExpired()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (now - _lastEviction < EvictionInterval)
            return;

        _lastEviction = now;
        foreach (KeyValuePair<string, (string Value, DateTimeOffset Expiry)> kvp in _store)
        {
            if (kvp.Value.Expiry <= now)
                _store.TryRemove(kvp);
        }
    }
}
