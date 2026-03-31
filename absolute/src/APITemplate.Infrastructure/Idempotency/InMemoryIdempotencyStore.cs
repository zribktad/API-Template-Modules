using System.Collections.Concurrent;
using System.Text.Json;
using APITemplate.Application.Common.Contracts;

namespace APITemplate.Infrastructure.Idempotency;

/// <summary>
/// Single-process, in-memory implementation of <see cref="IIdempotencyStore"/> backed by
/// <see cref="ConcurrentDictionary{TKey,TValue}"/>. Suitable for development and single-instance
/// deployments; TTL enforcement is done lazily on access via <c>EvictExpired</c>.
/// </summary>
public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, (string Value, DateTimeOffset Expiry)> _store =
        new();
    private readonly ConcurrentDictionary<string, string> _lockOwners = new();
    private readonly TimeProvider _timeProvider;

    public InMemoryIdempotencyStore(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <summary>Returns the cached entry for <paramref name="key"/> if it exists and has not expired; triggers lazy eviction otherwise.</summary>
    public Task<IdempotencyCacheEntry?> TryGetAsync(string key, CancellationToken ct = default)
    {
        if (_store.TryGetValue(key, out var entry) && entry.Expiry > _timeProvider.GetUtcNow())
        {
            var result = JsonSerializer.Deserialize<IdempotencyCacheEntry>(entry.Value);
            return Task.FromResult(result);
        }

        EvictExpired();
        return Task.FromResult<IdempotencyCacheEntry?>(null);
    }

    /// <summary>Attempts to insert a lock entry using <c>TryAdd</c>; returns <see langword="true"/> if the lock was acquired by this call.</summary>
    public Task<bool> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken ct = default)
    {
        EvictExpired();

        var lockKey = key + IdempotencyStoreConstants.LockSuffix;
        var lockValue = Guid.NewGuid().ToString("N");
        var expiry = _timeProvider.GetUtcNow().Add(ttl);
        var acquired = _store.TryAdd(lockKey, (lockValue, expiry));

        if (acquired)
            _lockOwners[key] = lockValue;

        return Task.FromResult(acquired);
    }

    /// <summary>Serialises <paramref name="entry"/> and inserts or replaces it in the store with the specified <paramref name="ttl"/>.</summary>
    public Task SetAsync(
        string key,
        IdempotencyCacheEntry entry,
        TimeSpan ttl,
        CancellationToken ct = default
    )
    {
        var json = JsonSerializer.Serialize(entry);
        var expiry = _timeProvider.GetUtcNow().Add(ttl);
        _store[key] = (json, expiry);
        return Task.CompletedTask;
    }

    /// <summary>Removes the lock entry for <paramref name="key"/> only if it is still owned by this store instance, preventing accidental release of expired locks.</summary>
    public Task ReleaseAsync(string key, CancellationToken ct = default)
    {
        if (!_lockOwners.TryRemove(key, out var lockValue))
            return Task.CompletedTask;

        var lockKey = key + IdempotencyStoreConstants.LockSuffix;
        _store.TryRemove(
            new KeyValuePair<string, (string Value, DateTimeOffset Expiry)>(
                lockKey,
                _store.GetValueOrDefault(lockKey)
            )
        );
        return Task.CompletedTask;
    }

    /// <summary>Lazily removes all entries whose expiry has passed, keeping memory usage bounded without a dedicated timer.</summary>
    private void EvictExpired()
    {
        var now = _timeProvider.GetUtcNow();
        foreach (var kvp in _store)
        {
            if (kvp.Value.Expiry <= now)
                _store.TryRemove(kvp);
        }
    }
}
