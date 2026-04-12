using System.Security.Claims;
using Identity.Logging;
using Identity.Auth.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Auth.Security.Sessions;

/// <summary>
///     Owns the lifecycle of persisted BFF sessions, including creation from authentication
///     tickets, validation on load, mutation, and revocation.
/// </summary>
public sealed class BffSessionService : IBffSessionService, IBffSessionRevocationService
{
    private readonly BffOptions _options;
    private readonly IBffSessionPrincipalFactory _principalFactory;
    private readonly IBffSessionStore _sessionStore;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BffSessionService> _logger;

    public BffSessionService(
        IOptions<BffOptions> options,
        IBffSessionStore sessionStore,
        IBffSessionPrincipalFactory principalFactory,
        TimeProvider timeProvider,
        ILogger<BffSessionService> logger
    )
    {
        _options = options.Value;
        _sessionStore = sessionStore;
        _principalFactory = principalFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> CreateSessionAsync(
        AuthenticationTicket ticket,
        CancellationToken ct = default
    )
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        BffSessionRecord session = CreateSessionRecord(
            Guid.NewGuid().ToString("N"),
            ticket,
            now,
            version: 1
        );
        await _sessionStore.StoreAsync(session, ct);
        return session.SessionId;
    }

    /// <inheritdoc />
    public async Task<AuthenticationTicket?> GetTicketAsync(
        string sessionId,
        CancellationToken ct = default
    )
    {
        BffSessionRecord? session = await GetSessionAsync(sessionId, ct);
        if (session is null)
            return null;

        return _principalFactory.CreateTicket(session, AuthConstants.BffSchemes.Cookie);
    }

    /// <inheritdoc />
    public async Task<BffSessionRecord?> GetSessionAsync(
        string sessionId,
        CancellationToken ct = default
    )
    {
        BffSessionRecord? session = await _sessionStore.GetAsync(sessionId, ct);
        if (session is null)
            return null;

        if (
            session.Status == BffSessionStatus.Revoked
            || session.Status == BffSessionStatus.Expired
        )
            return null;

        if (IsMalformed(session))
        {
            await RevokeAsync(sessionId, BffSessionRevocationReason.SessionCorrupted, ct);
            return null;
        }

        if (HasRefreshTokenExpired(session))
        {
            await ExpireAsync(sessionId, ct);
            return null;
        }

        // Absolute lifetime is a security policy — limits the damage window of a compromised session.
        // Revoke forces re-authentication, but since the user still has an active Keycloak SSO session,
        // re-login is a transparent redirect chain (~200ms), not a password prompt.
        if (HasExceededAbsoluteLifetime(session))
        {
            await RevokeAsync(sessionId, BffSessionRevocationReason.AbsoluteLifetimeExceeded, ct);
            return null;
        }

        return session;
    }

    /// <inheritdoc />
    public async Task UpdateSessionFromTicketAsync(
        string sessionId,
        AuthenticationTicket ticket,
        CancellationToken ct = default
    )
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        await MutateSessionAsync(
            sessionId,
            currentSession =>
                CreateSessionRecord(
                    sessionId,
                    ticket,
                    currentSession.CreatedAtUtc,
                    currentSession.Version + 1,
                    lastRefreshedAtUtc: currentSession.LastRefreshedAtUtc,
                    currentSession.Status == BffSessionStatus.Revoked
                        ? currentSession.Status
                        : BffSessionStatus.Active,
                    currentSession.RevokedAtUtc,
                    currentSession.RevocationReason
                ) with
                {
                    LastSeenAtUtc = now,
                },
            ct
        );
    }

    /// <inheritdoc />
    public Task RevokeAsync(
        string sessionId,
        BffSessionRevocationReason reason,
        CancellationToken ct = default
    )
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        return MutateSessionAsync(
            sessionId,
            currentSession =>
                currentSession with
                {
                    Status = BffSessionStatus.Revoked,
                    RevokedAtUtc = now,
                    RevocationReason = reason,
                    LastSeenAtUtc = now,
                    Version = currentSession.Version + 1,
                },
            ct
        );
    }

    private async Task MutateSessionAsync(
        string sessionId,
        Func<BffSessionRecord, BffSessionRecord> mutate,
        CancellationToken ct
    )
    {
        const int MaxAttempts = 5;

        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            BffSessionRecord? currentSession = await _sessionStore.GetAsync(sessionId, ct);
            if (currentSession is null)
                return;

            BffSessionRecord updatedSession = mutate(currentSession);
            bool updated = await _sessionStore.TryUpdateAsync(
                updatedSession,
                currentSession.Version,
                ct
            );
            if (updated)
                return;
        }

        _logger.BffSessionMutationFailed(sessionId, MaxAttempts);
    }

    private BffSessionRecord CreateSessionRecord(
        string sessionId,
        AuthenticationTicket ticket,
        DateTimeOffset createdAtUtc,
        long version,
        DateTimeOffset? lastRefreshedAtUtc = null,
        BffSessionStatus status = BffSessionStatus.Active,
        DateTimeOffset? revokedAtUtc = null,
        BffSessionRevocationReason? revocationReason = null
    )
    {
        ClaimsPrincipal principal = ticket.Principal;
        DateTimeOffset now = _timeProvider.GetUtcNow();

        string? nameIdentifier = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        string subject =
            principal.FindFirstValue(AuthConstants.Claims.Subject)
            ?? nameIdentifier
            ?? string.Empty;

        string userId = nameIdentifier ?? subject;

        string accessToken =
            ticket.Properties.GetTokenValue(AuthConstants.CookieTokenNames.AccessToken)
            ?? string.Empty;
        string refreshToken =
            ticket.Properties.GetTokenValue(AuthConstants.CookieTokenNames.RefreshToken)
            ?? string.Empty;
        string? idToken = ticket.Properties.GetTokenValue(AuthConstants.CookieTokenNames.IdToken);

        DateTimeOffset accessTokenExpiresAtUtc = ResolveAccessTokenExpiry(ticket.Properties, now);

        return new BffSessionRecord
        {
            SessionId = sessionId,
            UserId = userId,
            Subject = subject,
            Provider = BffProviderType.Keycloak,
            TenantId = principal.FindFirstValue(AuthConstants.Claims.TenantId),
            Roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).Distinct().ToArray(),
            Email = principal.FindFirstValue(ClaimTypes.Email),
            DisplayName =
                principal.FindFirstValue(ClaimTypes.Name)
                ?? principal.FindFirstValue(AuthConstants.Claims.PreferredUsername),
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            IdToken = idToken,
            AccessTokenExpiresAtUtc = accessTokenExpiresAtUtc,
            RefreshTokenExpiresAtUtc = null,
            CreatedAtUtc = createdAtUtc,
            LastSeenAtUtc = now,
            LastRefreshedAtUtc = lastRefreshedAtUtc ?? now,
            Status = status,
            Version = version,
            RevokedAtUtc = revokedAtUtc,
            RevocationReason = revocationReason,
        };
    }

    private Task ExpireAsync(string sessionId, CancellationToken ct)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        return MutateSessionAsync(
            sessionId,
            currentSession =>
                currentSession with
                {
                    Status = BffSessionStatus.Expired,
                    LastSeenAtUtc = now,
                    Version = currentSession.Version + 1,
                },
            ct
        );
    }

    /// <summary>
    ///     Token-level check: the refresh token issued by the identity provider has expired.
    ///     Once expired, Keycloak will reject it and the session can never be refreshed again — it is permanently dead.
    /// </summary>
    private bool HasRefreshTokenExpired(BffSessionRecord session)
    {
        return session.RefreshTokenExpiresAtUtc.HasValue
            && _timeProvider.GetUtcNow() >= session.RefreshTokenExpiresAtUtc.Value;
    }

    /// <summary>
    ///     Application-level security policy: forces re-authentication after a configurable absolute lifetime,
    ///     even when the refresh token is still valid. Guards against long-lived sessions on shared or
    ///     compromised devices (e.g. stolen laptop, public terminal).
    /// </summary>
    private bool HasExceededAbsoluteLifetime(BffSessionRecord session)
    {
        DateTimeOffset absoluteExpiry = session.CreatedAtUtc.AddMinutes(
            _options.SessionAbsoluteLifetimeMinutes
        );
        return _timeProvider.GetUtcNow() >= absoluteExpiry;
    }

    private static bool IsMalformed(BffSessionRecord session)
    {
        return string.IsNullOrWhiteSpace(session.SessionId)
            || string.IsNullOrWhiteSpace(session.UserId)
            || string.IsNullOrWhiteSpace(session.Subject)
            || string.IsNullOrWhiteSpace(session.AccessToken);
    }

    private static DateTimeOffset ResolveAccessTokenExpiry(
        AuthenticationProperties properties,
        DateTimeOffset fallbackNow
    )
    {
        string? expiresAt = properties.GetTokenValue(AuthConstants.CookieTokenNames.ExpiresAt);
        if (expiresAt is not null && DateTimeOffset.TryParse(expiresAt, out DateTimeOffset parsed))
            return parsed;

        return fallbackNow;
    }
}
