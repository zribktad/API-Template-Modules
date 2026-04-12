using System.Collections.Concurrent;
using System.Text.Json;
using SharedKernel.Application.Contracts;

namespace APITemplate.Tests.Unit.Idempotency;

/// <summary>
///     Test-only, single-process in-memory <see cref="IIdempotencyStore" />. Lock key suffix matches
///     <c>SharedKernel.Infrastructure.Idempotency.IdempotencyStoreConstants.LockSuffix</c> (<c>:lock</c>).
/// </summary>
public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private const string LockSuffix = ":lock";

    private static readonly TimeSpan EvictionInterval = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<string, (string Value, DateTimeOffset Expiry)> _store =
        new();

    private readonly TimeProvider _timeProvider;
    private DateTimeOffset _lastEviction = DateTimeOffset.MinValue;

    public InMemoryIdempotencyStore(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

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

    public Task<string?> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken ct = default)
    {
        EvictExpired();

        DateTimeOffset now = _timeProvider.GetUtcNow();

        if (
            _store.TryGetValue(key, out (string Value, DateTimeOffset Expiry) existing)
            && existing.Expiry > now
        )
            return Task.FromResult<string?>(null);

        string lockKey = key + LockSuffix;
        string lockValue = Guid.NewGuid().ToString("N");
        DateTimeOffset expiry = now.Add(ttl);
        bool acquired = _store.TryAdd(lockKey, (lockValue, expiry));

        return Task.FromResult(acquired ? lockValue : null);
    }

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

    public Task ReleaseAsync(string key, string lockToken, CancellationToken ct = default)
    {
        string lockKey = key + LockSuffix;

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
