using Identity.Auth.Options;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Identity.Auth.Security.Sessions;

/// <summary>
///     PostgreSQL-primary BFF session store using only <see cref="IDistributedCache" /> (e.g. in-memory
///     or Redis via ASP.NET cache abstraction). Use when <c>IConnectionMultiplexer</c> is not registered.
/// </summary>
public sealed class PostgresDistributedCacheBffSessionStore : BffPostgresSessionStoreBase
{
    public PostgresDistributedCacheBffSessionStore(
        IDistributedCache cache,
        IServiceScopeFactory scopeFactory,
        IBffSessionTokenProtector tokenProtector,
        IOptions<BffOptions> options,
        TimeProvider timeProvider
    )
        : base(cache, scopeFactory, tokenProtector, timeProvider, options) { }

    protected override Task<string?> GetCachedPayloadAsync(string cacheKey, CancellationToken ct) =>
        GetDistributedCachePayloadSlidingAsync(DistributedCache, cacheKey, ct);
}
