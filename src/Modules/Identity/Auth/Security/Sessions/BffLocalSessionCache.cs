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
    private readonly CacheState? _state;
    private long _generation;

    public BffLocalSessionCache(IOptions<BffOptions> options)
    {
        BffOptions opts = options.Value;
        TimeSpan ttl = TimeSpan.FromSeconds(opts.LocalCacheTtlSeconds);
        if (ttl <= TimeSpan.Zero)
            return;

        MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = opts.LocalCacheMaxEntries });
        MemoryCacheEntryOptions entryOptions = new()
        {
            AbsoluteExpirationRelativeToNow = ttl,
            Size = 1,
        };
        _state = new CacheState(cache, entryOptions);
    }

    public long Generation => Interlocked.Read(ref _generation);

    public bool TryGet(string sessionId, [NotNullWhen(true)] out BffSessionRecord? record)
    {
        if (
            _state is not null
            && _state.Cache.TryGetValue(GetKey(sessionId), out BffSessionRecord? cached)
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
        if (_state is null)
            return;

        _state.Cache.Set(GetKey(sessionId), record, _state.EntryOptions);
    }

    public void Invalidate(string sessionId)
    {
        if (_state is null)
            return;

        Interlocked.Increment(ref _generation);
        _state.Cache.Remove(GetKey(sessionId));
    }

    public void Dispose()
    {
        _state?.Cache.Dispose();
    }

    private static string GetKey(string sessionId) => BffSessionCacheKeys.GetSessionKey(sessionId);

    private sealed record CacheState(MemoryCache Cache, MemoryCacheEntryOptions EntryOptions);
}
