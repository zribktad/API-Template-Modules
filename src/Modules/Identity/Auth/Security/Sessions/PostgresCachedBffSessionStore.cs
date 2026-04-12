using System.Text.Json;
using Identity.Auth.Entities;
using Identity.Auth.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SharedKernel.Infrastructure.Redis;
using StackExchange.Redis;

namespace Identity.Auth.Security.Sessions;

/// <summary>
///     PostgreSQL-primary session store with Redis read-through cache.
///     PostgreSQL is the durable source of truth; Redis provides a short-TTL
///     cache layer to keep the hot path off the database.
/// </summary>
public sealed class PostgresCachedBffSessionStore : IBffSessionStore
{
    private const string CacheKeyPrefix = "bff:session:";

    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer _multiplexer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBffSessionTokenProtector _tokenProtector;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _cacheTtl;
    private readonly DistributedCacheEntryOptions _cacheEntryOptions;

    public PostgresCachedBffSessionStore(
        IDistributedCache cache,
        IConnectionMultiplexer connectionMultiplexer,
        IServiceScopeFactory scopeFactory,
        IBffSessionTokenProtector tokenProtector,
        IOptions<BffOptions> options,
        TimeProvider timeProvider
    )
    {
        _cache = cache;
        _multiplexer = connectionMultiplexer;
        _scopeFactory = scopeFactory;
        _tokenProtector = tokenProtector;
        _timeProvider = timeProvider;
        _cacheTtl = TimeSpan.FromMinutes(options.Value.CacheTtlMinutes);
        _cacheEntryOptions = new DistributedCacheEntryOptions { SlidingExpiration = _cacheTtl };
    }

    /// <inheritdoc />
    public async Task<BffSessionRecord?> GetAsync(string sessionId, CancellationToken ct = default)
    {
        string cacheKey = GetCacheKey(sessionId);

        string? cachedPayload = await GetCachedPayloadAsync(cacheKey, ct);
        if (cachedPayload is not null)
        {
            BffSessionRecord? cached = DeserializeAndUnprotect(cachedPayload, sessionId);
            if (cached is not null)
                return cached;
        }

        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        IdentityDbContext dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        BffPersistedSession? entity = await dbContext
            .BffSessions.IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && !s.IsDeleted, ct);

        if (entity is null)
            return null;

        BffSessionRecord protectedRecord = BffSessionMapper.ToRecord(entity);
        string payload = SerializeRecord(protectedRecord);
        await SetCacheAsync(cacheKey, payload, ct);

        return _tokenProtector.Unprotect(protectedRecord, sessionId);
    }

    /// <inheritdoc />
    public async Task StoreAsync(BffSessionRecord session, CancellationToken ct = default)
    {
        BffSessionRecord protectedRecord = _tokenProtector.Protect(session);
        BffPersistedSession entity = BffSessionMapper.ToEntity(session, protectedRecord);

        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        IdentityDbContext dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        dbContext.BffSessions.Add(entity);
        await dbContext.SaveChangesAsync(ct);

        string cacheKey = GetCacheKey(session.SessionId);
        string payload = SerializeRecord(protectedRecord);
        await SetCacheAsync(cacheKey, payload, ct);
    }

    /// <inheritdoc />
    public async Task<bool> TryUpdateAsync(
        BffSessionRecord session,
        long expectedVersion,
        CancellationToken ct = default
    )
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        IdentityDbContext dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        BffPersistedSession? entity = await dbContext
            .BffSessions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.SessionId == session.SessionId && !s.IsDeleted, ct);

        if (entity is null)
            return false;

        // Application-level CAS: distinct from EF xmin, which guards PG-level races.
        if (entity.Version != expectedVersion)
            return false;

        BffSessionRecord protectedRecord = _tokenProtector.Protect(session);
        BffSessionMapper.ApplyToEntity(entity, session, protectedRecord);

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }

        string cacheKey = GetCacheKey(session.SessionId);
        string payload = SerializeRecord(protectedRecord);
        await SetCacheAsync(cacheKey, payload, ct);

        return true;
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string sessionId, CancellationToken ct = default)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        IdentityDbContext dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        BffPersistedSession? entity = await dbContext
            .BffSessions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && !s.IsDeleted, ct);

        if (entity is not null)
        {
            entity.IsDeleted = true;
            entity.DeletedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
            await dbContext.SaveChangesAsync(ct);
        }

        await _cache.RemoveAsync(GetCacheKey(sessionId), ct);
    }

    private async Task<string?> GetCachedPayloadAsync(string cacheKey, CancellationToken ct)
    {
        if (_multiplexer.IsConnected)
        {
            IDatabase database = _multiplexer.GetDatabase();
            RedisValue value = (RedisValue)
                await database.ScriptEvaluateAsync(
                    RedisLuaScripts.GetAndRefreshTtl,
                    new { key = cacheKey, ttlMs = (long)_cacheTtl.TotalMilliseconds }
                );
            return value.IsNull ? null : value.ToString();
        }

        string? payload = await _cache.GetStringAsync(cacheKey, ct);
        if (payload is not null)
            await _cache.RefreshAsync(cacheKey, ct);

        return payload;
    }

    private Task SetCacheAsync(string cacheKey, string payload, CancellationToken ct)
    {
        return _cache.SetStringAsync(cacheKey, payload, _cacheEntryOptions, ct);
    }

    private static string GetCacheKey(string sessionId)
    {
        return CacheKeyPrefix + sessionId;
    }

    private static string SerializeRecord(BffSessionRecord protectedRecord)
    {
        return JsonSerializer.Serialize(protectedRecord, BffSessionSerializerOptions.Instance);
    }

    private BffSessionRecord? DeserializeAndUnprotect(string payload, string sessionId)
    {
        BffSessionRecord? record = JsonSerializer.Deserialize<BffSessionRecord>(
            payload,
            BffSessionSerializerOptions.Instance
        );
        if (record is null)
            return null;

        return _tokenProtector.Unprotect(record, sessionId);
    }
}
