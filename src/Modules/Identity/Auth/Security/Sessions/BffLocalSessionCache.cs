using Identity.Auth.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Identity.Auth.Security.Sessions;

/// <summary>
///     <see cref="IMemoryCache" />-backed implementation of <see cref="IBffLocalSessionCache" /> that
///     applies a sliding TTL from <see cref="BffOptions.LocalCacheTtlSeconds" />. When the TTL is
///     non-positive the cache becomes a no-op so the behavior matches "local cache disabled".
/// </summary>
public sealed class BffLocalSessionCache : IBffLocalSessionCache, IDisposable
{
    private readonly MemoryCache? _cache;
    private readonly TimeSpan _ttl;

    public BffLocalSessionCache(IOptions<BffOptions> options)
    {
        BffOptions opts = options.Value;
        _ttl = TimeSpan.FromSeconds(opts.LocalCacheTtlSeconds);
        if (_ttl <= TimeSpan.Zero)
        {
            _cache = null;
            return;
        }

        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = opts.LocalCacheMaxEntries });
    }

    public bool TryGet(string sessionId, out BffSessionRecord? record)
    {
        if (_cache is null)
        {
            record = null;
            return false;
        }

        if (
            _cache.TryGetValue(GetKey(sessionId), out BffSessionRecord? cached)
            && cached is not null
        )
        {
            record = cached;
            return true;
        }

        record = null;
        return false;
    }

    public void Set(string sessionId, BffSessionRecord record)
    {
        if (_cache is null)
            return;

        MemoryCacheEntryOptions entryOptions = new()
        {
            AbsoluteExpirationRelativeToNow = _ttl,
            Size = 1,
        };
        _cache.Set(GetKey(sessionId), record, entryOptions);
    }

    public void Invalidate(string sessionId)
    {
        _cache?.Remove(GetKey(sessionId));
    }

    public void Clear()
    {
        _cache?.Clear();
    }

    public void Dispose()
    {
        _cache?.Dispose();
    }

    private static string GetKey(string sessionId) =>
        BffSessionCacheKeys.SessionKeyPrefix + sessionId;
}
