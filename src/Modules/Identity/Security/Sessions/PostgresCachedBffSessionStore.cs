using System.Security.Cryptography;
using System.Text.Json;
using Identity.Logging;
using Identity.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel.Infrastructure.Redis;
using StackExchange.Redis;

namespace Identity.Security.Sessions;

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
    private readonly IDataProtector _protector;
    private readonly ILogger<PostgresCachedBffSessionStore> _logger;
    private readonly TimeSpan _cacheTtl;
    private readonly DistributedCacheEntryOptions _cacheEntryOptions;

    public PostgresCachedBffSessionStore(
        IDistributedCache cache,
        IConnectionMultiplexer connectionMultiplexer,
        IServiceScopeFactory scopeFactory,
        IOptions<BffOptions> options,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<PostgresCachedBffSessionStore> logger
    )
    {
        _cache = cache;
        _multiplexer = connectionMultiplexer;
        _scopeFactory = scopeFactory;
        _protector = dataProtectionProvider.CreateProtector("bff:session:tokens");
        _logger = logger;
        _cacheTtl = TimeSpan.FromMinutes(options.Value.CacheTtlMinutes);
        _cacheEntryOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _cacheTtl,
        };
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

        BffSessionRecord protectedRecord = MapToRecord(entity);
        string payload = SerializeProtectedRecord(protectedRecord);
        await SetCacheAsync(cacheKey, payload, ct);

        return UnprotectRecord(protectedRecord, sessionId);
    }

    /// <inheritdoc />
    public async Task StoreAsync(BffSessionRecord session, CancellationToken ct = default)
    {
        BffSessionRecord protectedRecord = ProtectTokens(session);
        BffPersistedSession entity = MapToEntity(session, protectedRecord);

        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        IdentityDbContext dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        dbContext.BffSessions.Add(entity);
        await dbContext.SaveChangesAsync(ct);

        string cacheKey = GetCacheKey(session.SessionId);
        string payload = SerializeProtectedRecord(protectedRecord);
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

        // Check application-level optimistic concurrency via Version on BffSessionRecord.
        // The record's Version field tracks logical mutations (incremented by BffSessionService).
        // This is separate from EF's xmin concurrency — xmin guards against PG-level races,
        // while Version guards against application-level CAS semantics.
        if (entity.Version != expectedVersion)
            return false;

        BffSessionRecord protectedRecord = ProtectTokens(session);
        ApplyToEntity(entity, session, protectedRecord);

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }

        string cacheKey = GetCacheKey(session.SessionId);
        string payload = SerializeProtectedRecord(protectedRecord);
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
            entity.DeletedAtUtc = DateTime.UtcNow;
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

    private BffSessionRecord ProtectTokens(BffSessionRecord session)
    {
        return session with
        {
            AccessToken = Protect(session.AccessToken),
            RefreshToken = Protect(session.RefreshToken),
            IdToken = string.IsNullOrWhiteSpace(session.IdToken) ? null : Protect(session.IdToken),
        };
    }

    private static string SerializeProtectedRecord(BffSessionRecord protectedRecord)
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

        return UnprotectRecord(record, sessionId);
    }

    private BffSessionRecord? UnprotectRecord(BffSessionRecord record, string sessionId)
    {
        try
        {
            return record with
            {
                AccessToken = Unprotect(record.AccessToken),
                RefreshToken = Unprotect(record.RefreshToken),
                IdToken = record.IdToken is null ? null : Unprotect(record.IdToken),
            };
        }
        catch (CryptographicException ex)
        {
            _logger.BffSessionUnprotectFailed(ex, sessionId);
            return null;
        }
        catch (FormatException ex)
        {
            _logger.BffSessionPayloadMalformed(ex, sessionId);
            return null;
        }
    }

    private string Protect(string value)
    {
        byte[] plaintextBytes = System.Text.Encoding.UTF8.GetBytes(value);
        byte[] ciphertextBytes = _protector.Protect(plaintextBytes);
        return Convert.ToBase64String(ciphertextBytes);
    }

    private string Unprotect(string ciphertext)
    {
        byte[] ciphertextBytes = Convert.FromBase64String(ciphertext);
        byte[] plaintextBytes = _protector.Unprotect(ciphertextBytes);
        return System.Text.Encoding.UTF8.GetString(plaintextBytes);
    }

    private static BffPersistedSession MapToEntity(
        BffSessionRecord session,
        BffSessionRecord protectedRecord
    )
    {
        return new BffPersistedSession
        {
            Id = Guid.NewGuid(),
            SessionId = session.SessionId,
            UserId = session.UserId,
            Subject = session.Subject,
            Provider = session.Provider,
            TenantId = Guid.TryParse(session.TenantId, out Guid tenantId) ? tenantId : Guid.Empty,
            Roles = session.Roles,
            Email = session.Email,
            DisplayName = session.DisplayName,
            EncryptedAccessToken = protectedRecord.AccessToken,
            EncryptedRefreshToken = protectedRecord.RefreshToken,
            EncryptedIdToken = protectedRecord.IdToken,
            AccessTokenExpiresAtUtc = session.AccessTokenExpiresAtUtc,
            RefreshTokenExpiresAtUtc = session.RefreshTokenExpiresAtUtc,
            CreatedAtUtc = session.CreatedAtUtc,
            LastSeenAtUtc = session.LastSeenAtUtc,
            LastRefreshedAtUtc = session.LastRefreshedAtUtc,
            Status = session.Status,
            Version = session.Version,
            RevokedAtUtc = session.RevokedAtUtc,
            RevocationReason = session.RevocationReason,
        };
    }

    /// <summary>
    ///     Maps a persisted entity back to a <see cref="BffSessionRecord" /> with tokens still encrypted.
    ///     The caller is responsible for unprotecting tokens before returning to consumers.
    /// </summary>
    private BffSessionRecord MapToRecord(BffPersistedSession entity)
    {
        return new BffSessionRecord
        {
            SessionId = entity.SessionId,
            UserId = entity.UserId,
            Subject = entity.Subject,
            Provider = entity.Provider,
            TenantId = entity.TenantId == Guid.Empty ? null : entity.TenantId.ToString(),
            Roles = entity.Roles,
            Email = entity.Email,
            DisplayName = entity.DisplayName,
            AccessToken = entity.EncryptedAccessToken,
            RefreshToken = entity.EncryptedRefreshToken,
            IdToken = entity.EncryptedIdToken,
            AccessTokenExpiresAtUtc = entity.AccessTokenExpiresAtUtc,
            RefreshTokenExpiresAtUtc = entity.RefreshTokenExpiresAtUtc,
            CreatedAtUtc = entity.CreatedAtUtc,
            LastSeenAtUtc = entity.LastSeenAtUtc,
            LastRefreshedAtUtc = entity.LastRefreshedAtUtc,
            Status = entity.Status,
            Version = entity.Version,
            RevokedAtUtc = entity.RevokedAtUtc,
            RevocationReason = entity.RevocationReason,
        };
    }

    private static void ApplyToEntity(
        BffPersistedSession entity,
        BffSessionRecord session,
        BffSessionRecord protectedRecord
    )
    {
        entity.UserId = session.UserId;
        entity.Subject = session.Subject;
        entity.Provider = session.Provider;
        entity.TenantId = Guid.TryParse(session.TenantId, out Guid tenantId)
            ? tenantId
            : Guid.Empty;
        entity.Roles = session.Roles;
        entity.Email = session.Email;
        entity.DisplayName = session.DisplayName;
        entity.EncryptedAccessToken = protectedRecord.AccessToken;
        entity.EncryptedRefreshToken = protectedRecord.RefreshToken;
        entity.EncryptedIdToken = protectedRecord.IdToken;
        entity.AccessTokenExpiresAtUtc = session.AccessTokenExpiresAtUtc;
        entity.RefreshTokenExpiresAtUtc = session.RefreshTokenExpiresAtUtc;
        entity.LastSeenAtUtc = session.LastSeenAtUtc;
        entity.LastRefreshedAtUtc = session.LastRefreshedAtUtc;
        entity.Status = session.Status;
        entity.Version = session.Version;
        entity.RevokedAtUtc = session.RevokedAtUtc;
        entity.RevocationReason = session.RevocationReason;
    }
}
