using System.Text.Json;
using Identity.Auth.Entities;
using Identity.Auth.Options;
using Identity.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace Identity.Auth.Security.Sessions;

/// <summary>
///     Shared PostgreSQL-backed BFF session persistence and cache write-through; subclasses only
///     differ in how the distributed cache payload is read (Redis Lua vs. plain cache API).
///     Consumers should depend on <see cref="IBffSessionStore" />; inheritance is reserved for implementations
///     in this assembly.
/// </summary>
public abstract class BffPostgresSessionStoreBase : IBffSessionStore
{
    private readonly IBffSessionDbContextFactory _dbContextFactory;
    private readonly IBffSessionTokenProtector _tokenProtector;
    private readonly TimeProvider _timeProvider;
    private readonly DistributedCacheEntryOptions _cacheEntryOptions;

    protected BffPostgresSessionStoreBase(
        IDistributedCache cache,
        IBffSessionDbContextFactory dbContextFactory,
        IBffSessionTokenProtector tokenProtector,
        TimeProvider timeProvider,
        IOptions<BffOptions> options
    )
    {
        DistributedCache = cache;
        _dbContextFactory = dbContextFactory;
        _tokenProtector = tokenProtector;
        _timeProvider = timeProvider;
        TimeSpan cacheTtl = TimeSpan.FromMinutes(options.Value.CacheTtlMinutes);
        CacheTtl = cacheTtl;
        _cacheEntryOptions = new DistributedCacheEntryOptions { SlidingExpiration = cacheTtl };
    }

    protected IDistributedCache DistributedCache { get; }

    /// <summary>Sliding TTL for cache entries; used by Redis Lua read-through in subclasses.</summary>
    protected TimeSpan CacheTtl { get; }

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

        await using IBffSessionDbContextLease lease = _dbContextFactory.Create();
        IdentityDbContext dbContext = lease.DbContext;

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

        await using IBffSessionDbContextLease lease = _dbContextFactory.Create();
        IdentityDbContext dbContext = lease.DbContext;

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
        await using IBffSessionDbContextLease lease = _dbContextFactory.Create();
        IdentityDbContext dbContext = lease.DbContext;

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
        await using IBffSessionDbContextLease lease = _dbContextFactory.Create();
        IdentityDbContext dbContext = lease.DbContext;

        BffPersistedSession? entity = await dbContext
            .BffSessions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && !s.IsDeleted, ct);

        if (entity is not null)
        {
            entity.IsDeleted = true;
            entity.DeletedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
            await dbContext.SaveChangesAsync(ct);
        }

        await DistributedCache.RemoveAsync(GetCacheKey(sessionId), ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> FindActiveSessionIdsBySubjectAsync(
        string keycloakSubject,
        CancellationToken ct = default
    )
    {
        await using IBffSessionDbContextLease lease = _dbContextFactory.Create();
        IdentityDbContext dbContext = lease.DbContext;

        List<string> ids = await dbContext
            .BffSessions.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s =>
                s.Subject == keycloakSubject
                && !s.IsDeleted
                && s.Status != BffSessionStatus.Revoked
                && s.Status != BffSessionStatus.Expired
            )
            .Select(s => s.SessionId)
            .ToListAsync(ct);

        return ids;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> BulkRevokeActiveSessionsBySubjectAsync(
        string keycloakSubject,
        BffSessionRevocationReason reason,
        DateTimeOffset revokedAtUtc,
        CancellationToken ct = default
    )
    {
        await using IBffSessionDbContextLease lease = _dbContextFactory.Create();
        IdentityDbContext dbContext = lease.DbContext;

        List<string> sessionIds = await dbContext
            .BffSessions.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s =>
                s.Subject == keycloakSubject
                && !s.IsDeleted
                && s.Status != BffSessionStatus.Revoked
                && s.Status != BffSessionStatus.Expired
            )
            .Select(s => s.SessionId)
            .ToListAsync(ct);

        if (sessionIds.Count == 0)
            return sessionIds;

        await dbContext
            .BffSessions.IgnoreQueryFilters()
            .Where(s => sessionIds.Contains(s.SessionId))
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(s => s.Status, BffSessionStatus.Revoked)
                        .SetProperty(s => s.RevokedAtUtc, revokedAtUtc)
                        .SetProperty(s => s.RevocationReason, reason)
                        .SetProperty(s => s.LastSeenAtUtc, revokedAtUtc)
                        .SetProperty(s => s.Version, s => s.Version + 1),
                ct
            );

        foreach (string sessionId in sessionIds)
            await DistributedCache.RemoveAsync(GetCacheKey(sessionId), ct);

        return sessionIds;
    }

    protected abstract Task<string?> GetCachedPayloadAsync(string cacheKey, CancellationToken ct);

    /// <summary>
    ///     Reads via IDistributedCache.GetStringAsync; sliding expiration
    ///     is applied when this store writes entries using <see cref="DistributedCacheEntryOptions" /> from options.
    /// </summary>
    protected static Task<string?> GetDistributedCachePayloadSlidingAsync(
        IDistributedCache cache,
        string cacheKey,
        CancellationToken ct
    )
    {
        return cache.GetStringAsync(cacheKey, ct);
    }

    private Task SetCacheAsync(string cacheKey, string payload, CancellationToken ct)
    {
        return DistributedCache.SetStringAsync(cacheKey, payload, _cacheEntryOptions, ct);
    }

    private static string GetCacheKey(string sessionId) =>
        BffSessionCacheKeys.GetSessionKey(sessionId);

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
