using System.Security.Cryptography;
using System.Text.Json;
using Identity.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Identity.Security.Sessions;

/// <summary>
///     Dragonfly/Redis-backed session store that persists BFF session records as protected JSON
///     payloads and uses optimistic concurrency for mutations.
/// </summary>
public sealed class DragonflyBffSessionStore : IBffSessionStore
{
    private const string SessionKeyPrefix = "bff:session:";
    internal static readonly JsonSerializerOptions SerializerOptions = new(
        JsonSerializerDefaults.Web
    );

    private static readonly LuaScript GetAndRefreshTtlScript = LuaScript.Prepare(
        """
        local val = redis.call('get', @key)
        if val then redis.call('pexpire', @key, @ttlMs) end
        return val
        """
    );

    private static readonly LuaScript CompareAndSetScript = LuaScript.Prepare(
        """
        local current = redis.call('get', @key)
        if current == false then
            return -1
        end

        local decoded = cjson.decode(current)
        if tonumber(decoded.version) ~= tonumber(@expectedVersion) then
            return 0
        end

        redis.call('psetex', @key, @ttlMs, @value)
        return 1
        """
    );

    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer? _multiplexer;
    private readonly BffOptions _options;
    private readonly IDataProtector _protector;
    private readonly ILogger<DragonflyBffSessionStore> _logger;

    public DragonflyBffSessionStore(
        IDistributedCache cache,
        IEnumerable<IConnectionMultiplexer> connectionMultiplexers,
        IOptions<BffOptions> options,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<DragonflyBffSessionStore> logger
    )
    {
        _cache = cache;
        _multiplexer = connectionMultiplexers.FirstOrDefault();
        _options = options.Value;
        _protector = dataProtectionProvider.CreateProtector("bff:session:tokens");
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BffSessionRecord?> GetAsync(string sessionId, CancellationToken ct = default)
    {
        string key = GetSessionKey(sessionId);

        string? payload = await GetAndRefreshPayloadAsync(key, ct);
        if (payload is null)
            return null;

        BffSessionRecord? storedRecord = JsonSerializer.Deserialize<BffSessionRecord>(
            payload,
            SerializerOptions
        );
        if (storedRecord is null)
            return null;

        try
        {
            return storedRecord with
            {
                AccessToken = Unprotect(storedRecord.AccessToken),
                RefreshToken = Unprotect(storedRecord.RefreshToken),
                IdToken = storedRecord.IdToken is null ? null : Unprotect(storedRecord.IdToken),
            };
        }
        catch (CryptographicException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to unprotect session {SessionId} tokens — possible key rotation",
                sessionId
            );
            return null;
        }
        catch (FormatException ex)
        {
            _logger.LogWarning(
                ex,
                "Malformed protected payload for session {SessionId}",
                sessionId
            );
            return null;
        }
    }

    /// <inheritdoc />
    public Task StoreAsync(BffSessionRecord session, CancellationToken ct = default)
    {
        return SetAsync(session, ct);
    }

    /// <inheritdoc />
    public async Task<bool> TryUpdateAsync(
        BffSessionRecord session,
        long expectedVersion,
        CancellationToken ct = default
    )
    {
        string key = GetSessionKey(session.SessionId);
        string payload = SerializeForStorage(session);

        if (_multiplexer is null || !_multiplexer.IsConnected)
        {
            BffSessionRecord? currentSession = await GetAsync(session.SessionId, ct);
            if (currentSession is null || currentSession.Version != expectedVersion)
                return false;

            await SetRawPayloadAsync(key, payload, ct);
            return true;
        }

        IDatabase database = _multiplexer.GetDatabase();
        long result = (long)
            await database.ScriptEvaluateAsync(
                CompareAndSetScript,
                new
                {
                    key,
                    value = payload,
                    expectedVersion,
                    ttlMs = GetIdleTimeout().TotalMilliseconds,
                }
            );

        return result == 1;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string sessionId, CancellationToken ct = default)
    {
        return _cache.RemoveAsync(GetSessionKey(sessionId), ct);
    }

    private Task SetAsync(BffSessionRecord session, CancellationToken ct)
    {
        return SetRawPayloadAsync(
            GetSessionKey(session.SessionId),
            SerializeForStorage(session),
            ct
        );
    }

    private async Task SetRawPayloadAsync(string key, string payload, CancellationToken ct)
    {
        DistributedCacheEntryOptions entryOptions = new()
        {
            AbsoluteExpirationRelativeToNow = GetIdleTimeout(),
        };

        await _cache.SetStringAsync(key, payload, entryOptions, ct);
    }

    private async Task<string?> GetAndRefreshPayloadAsync(string key, CancellationToken ct)
    {
        if (_multiplexer is not null && _multiplexer.IsConnected)
        {
            IDatabase database = _multiplexer.GetDatabase();
            RedisValue value = (RedisValue)
                await database.ScriptEvaluateAsync(
                    GetAndRefreshTtlScript,
                    new { key, ttlMs = (long)GetIdleTimeout().TotalMilliseconds }
                );
            return value.IsNull ? null : value.ToString();
        }

        string? payload = await _cache.GetStringAsync(key, ct);
        if (payload is not null)
            await _cache.RefreshAsync(key, ct);

        return payload;
    }

    private string SerializeForStorage(BffSessionRecord session)
    {
        BffSessionRecord protectedRecord = session with
        {
            AccessToken = Protect(session.AccessToken),
            RefreshToken = Protect(session.RefreshToken),
            IdToken = string.IsNullOrWhiteSpace(session.IdToken) ? null : Protect(session.IdToken),
        };

        return JsonSerializer.Serialize(protectedRecord, SerializerOptions);
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

    private TimeSpan GetIdleTimeout()
    {
        return TimeSpan.FromMinutes(_options.GetEffectiveSessionIdleTimeoutMinutes());
    }

    private static string GetSessionKey(string sessionId)
    {
        return SessionKeyPrefix + sessionId;
    }
}
