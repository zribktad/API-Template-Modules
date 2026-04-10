using System.Security.Claims;
using Identity.Logging;
using Identity.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Security.Sessions;

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
        const int MaxAttempts = 3;

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
