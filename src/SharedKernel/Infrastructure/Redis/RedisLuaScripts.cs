using StackExchange.Redis;

namespace SharedKernel.Infrastructure.Redis;

/// <summary>
///     Shared Lua scripts for common Redis atomic operations used across infrastructure services.
/// </summary>
public static class RedisLuaScripts
{
    /// <summary>
    ///     Atomically releases a distributed lock only if the caller still owns it.
    ///     Parameters: <c>@key</c> (lock key), <c>@value</c> (owner token).
    ///     Returns 1 on success, 0 if the lock was already released or owned by another holder.
    /// </summary>
    public static readonly LuaScript ReleaseLock = LuaScript.Prepare(
        "if redis.call('get', @key) == @value then return redis.call('del', @key) else return 0 end"
    );

    /// <summary>
    ///     Atomically reads a key and refreshes its TTL when the key exists.
    ///     Parameters: <c>@key</c> (cache key), <c>@ttlMs</c> (TTL in milliseconds).
    ///     Returns the value if it exists, nil otherwise.
    /// </summary>
    public static readonly LuaScript GetAndRefreshTtl = LuaScript.Prepare(
        """
        local val = redis.call('get', @key)
        if val then redis.call('pexpire', @key, @ttlMs) end
        return val
        """
    );
}
