using System.Diagnostics.CodeAnalysis;
using Identity.Auth.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Identity.Auth.Security.Sessions;

/// <summary>
///     <see cref="IMemoryCache" />-backed implementation of <see cref="IBffLocalSessionCache" /> that
///     applies an absolute TTL from <see cref="BffOptions.LocalCacheTtlSeconds" /> relative to each
///     write — repeated reads do not extend the lifetime. When the TTL is non-positive the cache
///     becomes a no-op so the behavior matches "local cache disabled".
/// </summary>
public sealed class BffLocalSessionCache : IBffLocalSessionCache, IDisposable
{
    private readonly MemoryCache? _cache;
    private readonly MemoryCacheEntryOptions? _entryOptions;
    private long _generation;

    public BffLocalSessionCache(IOptions<BffOptions> options)
    {
        BffOptions opts = options.Value;
        TimeSpan ttl = TimeSpan.FromSeconds(opts.LocalCacheTtlSeconds);
        if (ttl <= TimeSpan.Zero)
        {
            _cache = null;
            _entryOptions = null;
            return;
        }

        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = opts.LocalCacheMaxEntries });
        _entryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl,
            Size = 1,
        };
    }

    public long Generation => Interlocked.Read(ref _generation);

    public bool TryGet(string sessionId, [NotNullWhen(true)] out BffSessionRecord? record)
    {
        if (
            _cache is not null
            && _cache.TryGetValue(GetKey(sessionId), out BffSessionRecord? cached)
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
        if (_cache is null || _entryOptions is null)
            return;

        _cache.Set(GetKey(sessionId), record, _entryOptions);
    }

    public void Invalidate(string sessionId)
    {
        if (_cache is null)
            return;

        Interlocked.Increment(ref _generation);
        _cache.Remove(GetKey(sessionId));
    }

    public void Dispose()
    {
        _cache?.Dispose();
    }

    private static string GetKey(string sessionId) =>
        BffSessionCacheKeys.SessionKeyPrefix + sessionId;
}
