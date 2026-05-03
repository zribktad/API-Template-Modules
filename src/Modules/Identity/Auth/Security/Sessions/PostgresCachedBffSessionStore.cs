using BuildingBlocks.Infrastructure.Redis.Redis;
using Identity.Auth.Options;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Identity.Auth.Security.Sessions;

/// <summary>
///     PostgreSQL-primary session store with Redis read-through cache.
///     PostgreSQL is the durable source of truth; Redis provides a short-TTL
///     cache layer to keep the hot path off the database.
/// </summary>
public sealed class PostgresCachedBffSessionStore : BffPostgresSessionStoreBase
{
    private readonly IConnectionMultiplexer _multiplexer;

    public PostgresCachedBffSessionStore(
        IDistributedCache cache,
        IConnectionMultiplexer connectionMultiplexer,
        IBffSessionDbContextFactory dbContextFactory,
        IBffSessionTokenProtector tokenProtector,
        IOptions<BffOptions> options,
        TimeProvider timeProvider
    )
        : base(cache, dbContextFactory, tokenProtector, timeProvider, options) =>
        _multiplexer = connectionMultiplexer;

    protected override async Task<string?> GetCachedPayloadAsync(
        string cacheKey,
        CancellationToken ct
    )
    {
        if (_multiplexer.IsConnected)
        {
            IDatabase database = _multiplexer.GetDatabase();
            RedisValue value = (RedisValue)
                await database.ScriptEvaluateAsync(
                    RedisLuaScripts.GetAndRefreshTtl,
                    new { key = cacheKey, ttlMs = (long)CacheTtl.TotalMilliseconds }
                );
            return value.IsNull ? null : value.ToString();
        }

        return await GetDistributedCachePayloadSlidingAsync(DistributedCache, cacheKey, ct);
    }
}
