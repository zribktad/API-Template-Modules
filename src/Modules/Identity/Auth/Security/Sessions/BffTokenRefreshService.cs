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
    private readonly IBffSessionRevocationService _revocationService;
    private readonly IBffSessionStore _sessionStore;
    private readonly IKeycloakService _keycloakService;
    private readonly BffOptions _options;
    private readonly TimeProvider _timeProvider;

    public BffTokenRefreshService(
        IBffRefreshCoordinator refreshCoordinator,
        IBffSessionStore sessionStore,
        IBffSessionRevocationService revocationService,
        IKeycloakService keycloakService,
        IOptions<BffOptions> options,
        TimeProvider timeProvider
    )
    {
        _refreshCoordinator = refreshCoordinator;
        _sessionStore = sessionStore;
        _revocationService = revocationService;
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

        if (string.IsNullOrWhiteSpace(session.RefreshToken))
        {
            await _revocationService.RevokeAsync(
                session.SessionId,
                BffSessionRevocationReason.RefreshTokenMissing,
                ct
            );
            return BffRefreshOutcome.Failed(BffSessionRevocationReason.RefreshTokenMissing);
        }

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
        // Re-check after acquiring the lock — another leader may have refreshed already.
        if (!IsRefreshRequired(currentSession))
            return BffRefreshOutcome.NotRequired(currentSession);

        KeycloakRefreshResult refreshResult = await _keycloakService.RefreshSessionAsync(
            currentSession.RefreshToken,
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
                await _revocationService.RevokeAsync(currentSession.SessionId, reason, ct);

            return BffRefreshOutcome.Failed(reason);
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();

        DateTimeOffset? refreshTokenExpiresAtUtc = refreshResult.TokenResponse.RefreshExpiresIn
            is > 0
            ? now.AddSeconds(refreshResult.TokenResponse.RefreshExpiresIn.Value)
            : currentSession.RefreshTokenExpiresAtUtc;

        BffSessionRecord updatedSession = currentSession with
        {
            AccessToken = refreshResult.TokenResponse.AccessToken,
            RefreshToken = refreshResult.TokenResponse.RefreshToken ?? currentSession.RefreshToken,
            AccessTokenExpiresAtUtc = now.AddSeconds(refreshResult.TokenResponse.ExpiresIn),
            RefreshTokenExpiresAtUtc = refreshTokenExpiresAtUtc,
            LastRefreshedAtUtc = now,
            LastSeenAtUtc = now,
            Status = BffSessionStatus.Active,
            Version = currentSession.Version + 1,
        };

        bool updated = await _sessionStore.TryUpdateAsync(
            updatedSession,
            currentSession.Version,
            ct
        );
        if (!updated)
            return await ReloadFollowerOutcomeAsync(currentSession.SessionId, ct);

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
            return BffRefreshOutcome.Failed(BffSessionRevocationReason.RefreshRejected);

        return BffRefreshOutcome.Success(reloadedSession);
    }
}
