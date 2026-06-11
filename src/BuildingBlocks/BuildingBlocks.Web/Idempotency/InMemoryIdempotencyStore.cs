using BuildingBlocks.Application.Contracts;
using Microsoft.Extensions.Caching.Memory;

namespace BuildingBlocks.Web.Idempotency;

public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly IMemoryCache _cache;
    private static readonly object LockValue = new();

    public InMemoryIdempotencyStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<IdempotencyCacheEntry?> TryGetAsync(string key, CancellationToken ct = default)
    {
        _cache.TryGetValue(key, out IdempotencyCacheEntry? entry);
        return Task.FromResult(entry);
    }

    public Task<string?> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken ct = default)
    {
        string lockKey = "lock:" + key;

        if (_cache.TryGetValue(lockKey, out _))
            return Task.FromResult<string?>(null);

        string token = Guid.NewGuid().ToString();
        _cache.Set(lockKey, token, ttl);

        return Task.FromResult<string?>(token);
    }

    public Task ReleaseAsync(string key, string lockToken, CancellationToken ct = default)
    {
        string lockKey = "lock:" + key;
        if (_cache.TryGetValue(lockKey, out string? token) && token == lockToken)
        {
            _cache.Remove(lockKey);
        }
        return Task.CompletedTask;
    }

    public Task SetAsync(
        string key,
        IdempotencyCacheEntry entry,
        TimeSpan ttl,
        CancellationToken ct = default
    )
    {
        _cache.Set(key, entry, ttl);
        return Task.CompletedTask;
    }
}
