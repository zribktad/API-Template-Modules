using Identity.Auth.Options;
using Identity.Auth.Security.Keycloak;
using Microsoft.Extensions.Options;

namespace Identity.Auth.Security.Sessions;

/// <summary>
///     Decides when a BFF session requires token refresh and coordinates refresh execution,
///     provider interaction, and session revocation on failure.
/// </summary>
public sealed class BffTokenRefreshService : IBffTokenRefreshService
{
    private readonly IBffRefreshCoordinator _refreshCoordinator;
    private readonly IBffSessionStore _sessionStore;
    private readonly IKeycloakService _keycloakService;
    private readonly BffOptions _options;
    private readonly TimeProvider _timeProvider;

    public BffTokenRefreshService(
        IBffRefreshCoordinator refreshCoordinator,
        IBffSessionStore sessionStore,
        IKeycloakService keycloakService,
        IOptions<BffOptions> options,
        TimeProvider timeProvider
    )
    {
        _refreshCoordinator = refreshCoordinator;
        _sessionStore = sessionStore;
        _keycloakService = keycloakService;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<BffRefreshOutcome> RefreshIfRequiredAsync(
        BffSessionRecord session,
        CancellationToken ct = default
    )
    {
        if (!IsRefreshRequired(session))
            return BffRefreshOutcome.NotRequired(session);

        return await _refreshCoordinator.ExecuteAsync(
            session.SessionId,
            leaderCt => RefreshAsLeaderAsync(session, leaderCt),
            followerCt => ReloadFollowerOutcomeAsync(session.SessionId, followerCt),
            ct
        );
    }

    private bool IsRefreshRequired(BffSessionRecord session)
    {
        return session.AccessTokenExpiresAtUtc - _timeProvider.GetUtcNow()
            <= TimeSpan.FromMinutes(_options.RefreshThresholdMinutes);
    }

    private async Task<BffRefreshOutcome> RefreshAsLeaderAsync(
        BffSessionRecord currentSession,
        CancellationToken ct
    )
    {
        BffSessionRecord? latestSession = await _sessionStore.GetAsync(
            currentSession.SessionId,
            ct
        );
        if (latestSession is null)
            return BffRefreshOutcome.Failed(BffSessionRevocationReason.SessionCorrupted);

        if (latestSession.Status == BffSessionStatus.Revoked)
        {
            return BffRefreshOutcome.Failed(
                latestSession.RevocationReason ?? BffSessionRevocationReason.RefreshRejected
            );
        }

        if (!IsRefreshRequired(latestSession))
            return BffRefreshOutcome.NotRequired(latestSession);

        if (string.IsNullOrWhiteSpace(latestSession.RefreshToken))
        {
            return await RevokeOrReloadAsync(
                latestSession,
                BffSessionRevocationReason.RefreshTokenMissing,
                ct
            );
        }

        KeycloakRefreshResult refreshResult = await _keycloakService.RefreshSessionAsync(
            latestSession.RefreshToken,
            ct
        );

        if (
            refreshResult.Status != KeycloakRefreshStatus.Success
            || refreshResult.TokenResponse is null
        )
        {
            BffSessionRevocationReason reason =
                refreshResult.Status == KeycloakRefreshStatus.Rejected
                    ? BffSessionRevocationReason.RefreshRejected
                    : BffSessionRevocationReason.ProviderSessionInvalid;

            if (_options.RevokeSessionOnRefreshFailure)
                return await RevokeOrReloadAsync(latestSession, reason, ct);

            return BffRefreshOutcome.Failed(reason);
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();

        DateTimeOffset? refreshTokenExpiresAtUtc = refreshResult.TokenResponse.RefreshExpiresIn
            is > 0
            ? now.AddSeconds(refreshResult.TokenResponse.RefreshExpiresIn.Value)
            : latestSession.RefreshTokenExpiresAtUtc;

        BffSessionRecord updatedSession = latestSession with
        {
            AccessToken = refreshResult.TokenResponse.AccessToken,
            RefreshToken = refreshResult.TokenResponse.RefreshToken ?? latestSession.RefreshToken,
            AccessTokenExpiresAtUtc = now.AddSeconds(refreshResult.TokenResponse.ExpiresIn),
            RefreshTokenExpiresAtUtc = refreshTokenExpiresAtUtc,
            LastRefreshedAtUtc = now,
            LastSeenAtUtc = now,
            Status = BffSessionStatus.Active,
            Version = latestSession.Version + 1,
        };

        bool updated = await _sessionStore.TryUpdateAsync(
            updatedSession,
            latestSession.Version,
            ct
        );
        if (!updated)
            return await ReloadFollowerOutcomeAsync(latestSession.SessionId, ct);

        return BffRefreshOutcome.Success(updatedSession);
    }

    private async Task<BffRefreshOutcome> ReloadFollowerOutcomeAsync(
        string sessionId,
        CancellationToken ct
    )
    {
        BffSessionRecord? reloadedSession = await _sessionStore.GetAsync(sessionId, ct);
        if (reloadedSession is null)
            return BffRefreshOutcome.Failed(BffSessionRevocationReason.SessionCorrupted);

        if (reloadedSession.Status == BffSessionStatus.Revoked)
        {
            return BffRefreshOutcome.Failed(
                reloadedSession.RevocationReason ?? BffSessionRevocationReason.RefreshRejected
            );
        }

        if (IsRefreshRequired(reloadedSession))
        {
            return HasAccessTokenExpired(reloadedSession)
                ? BffRefreshOutcome.Failed(BffSessionRevocationReason.RefreshRejected)
                : BffRefreshOutcome.NotRequired(reloadedSession);
        }

        return BffRefreshOutcome.Success(reloadedSession);
    }

    private bool HasAccessTokenExpired(BffSessionRecord session)
    {
        return session.AccessTokenExpiresAtUtc <= _timeProvider.GetUtcNow();
    }

    private async Task<BffRefreshOutcome> RevokeOrReloadAsync(
        BffSessionRecord expectedSession,
        BffSessionRevocationReason reason,
        CancellationToken ct
    )
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        BffSessionRecord revokedSession = expectedSession with
        {
            Status = BffSessionStatus.Revoked,
            RevokedAtUtc = now,
            RevocationReason = reason,
            LastSeenAtUtc = now,
            Version = expectedSession.Version + 1,
        };

        bool updated = await _sessionStore.TryUpdateAsync(
            revokedSession,
            expectedSession.Version,
            ct
        );
        if (updated)
            return BffRefreshOutcome.Failed(reason);

        return await ReloadFollowerOutcomeAsync(expectedSession.SessionId, ct);
    }
}
