using System.Text.Json;
using SharedKernel.Application.Contracts;
using StackExchange.Redis;

namespace SharedKernel.Infrastructure.Idempotency;

/// <summary>
///     Redis/Dragonfly-backed implementation of <see cref="IIdempotencyStore" /> that stores
///     idempotency cache entries and distributed locks using atomic Lua scripts.
///     Suitable for multi-instance deployments where in-process state would cause duplicate processing.
/// </summary>
public sealed class DistributedCacheIdempotencyStore : IIdempotencyStore
{
    private const string KeyPrefix = "idempotency:";

    private static readonly LuaScript ReleaseLockScript = LuaScript.Prepare(
        "if redis.call('get', @key) == @value then return redis.call('del', @key) else return 0 end"
    );

    private static readonly LuaScript AcquireLockScript = LuaScript.Prepare(
        "if redis.call('exists', @resultKey) == 1 then return nil end "
            + "if redis.call('set', @lockKey, @lockValue, 'NX', 'PX', @ttlMs) then return @lockValue else return nil end"
    );

    private readonly IDatabase _database;

    public DistributedCacheIdempotencyStore(IConnectionMultiplexer connectionMultiplexer)
    {
        _database = connectionMultiplexer.GetDatabase();
    }

    /// <summary>
    ///     Returns the cached entry for <paramref name="key" /> if it exists in Redis, or <see langword="null" /> if
    ///     absent or expired.
    /// </summary>
    public async Task<IdempotencyCacheEntry?> TryGetAsync(
        string key,
        CancellationToken ct = default
    )
    {
        RedisValue json = await _database.StringGetAsync(KeyPrefix + key);
        return json.IsNullOrEmpty
            ? null
            : JsonSerializer.Deserialize<IdempotencyCacheEntry>(json.ToString());
    }

    /// <summary>
    ///     Attempts to set a lock key in Redis using SET NX with the given <paramref name="ttl" />.
    ///     Returns <see langword="true" /> if the lock was acquired; the lock value is stored locally for later release.
    /// </summary>
    public async Task<string?> TryAcquireAsync(
        string key,
        TimeSpan ttl,
        CancellationToken ct = default
    )
    {
        string resultKey = KeyPrefix + key;
        string lockKey = resultKey + IdempotencyStoreConstants.LockSuffix;
        string lockValue = Guid.NewGuid().ToString("N");
        long ttlMs = (long)ttl.TotalMilliseconds;

        RedisResult result = await _database.ScriptEvaluateAsync(
            AcquireLockScript,
            new
            {
                resultKey = (RedisKey)resultKey,
                lockKey = (RedisKey)lockKey,
                lockValue,
                ttlMs,
            }
        );

        return result.IsNull ? null : lockValue;
    }

    /// <summary>
    ///     Serialises <paramref name="entry" /> and stores it under <paramref name="key" /> in Redis with the specified
    ///     <paramref name="ttl" />.
    /// </summary>
    public async Task SetAsync(
        string key,
        IdempotencyCacheEntry entry,
        TimeSpan ttl,
        CancellationToken ct = default
    )
    {
        string json = JsonSerializer.Serialize(entry);
        await _database.StringSetAsync(KeyPrefix + key, json, ttl);
    }

    /// <summary>
    ///     Releases the lock for <paramref name="key" /> using an atomic Lua compare-and-delete script to prevent
    ///     releasing a lock owned by another instance.
    /// </summary>
    public async Task ReleaseAsync(string key, string lockToken, CancellationToken ct = default)
    {
        string lockKey = KeyPrefix + key + IdempotencyStoreConstants.LockSuffix;
        await _database.ScriptEvaluateAsync(
            ReleaseLockScript,
            new { key = (RedisKey)lockKey, value = (RedisValue)lockToken }
        );
    }
}
