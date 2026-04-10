using Identity.Options;
using Identity.Security.Keycloak;
using Microsoft.Extensions.Options;

namespace Identity.Security.Sessions;

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
        string sessionId,
        CancellationToken ct = default
    )
    {
        BffSessionRecord? session = await _sessionStore.GetAsync(sessionId, ct);
        if (session is null)
            return BffRefreshOutcome.Failed(BffSessionRevocationReason.SessionCorrupted);

        if (
            session.Status == BffSessionStatus.Revoked
            || session.Status == BffSessionStatus.Expired
        )
            return BffRefreshOutcome.Failed(BffSessionRevocationReason.ProviderSessionInvalid);

        if (!IsRefreshRequired(session))
            return BffRefreshOutcome.NotRequired(session);

        if (string.IsNullOrWhiteSpace(session.RefreshToken))
        {
            await _revocationService.RevokeAsync(
                sessionId,
                BffSessionRevocationReason.RefreshTokenMissing,
                ct
            );
            return BffRefreshOutcome.Failed(BffSessionRevocationReason.RefreshTokenMissing);
        }

        return await _refreshCoordinator.ExecuteAsync(
            sessionId,
            async leaderCt => await RefreshAsLeaderAsync(sessionId, leaderCt),
            async followerCt => await ReloadFollowerOutcomeAsync(sessionId, followerCt),
            ct
        );
    }

    private bool IsRefreshRequired(BffSessionRecord session)
    {
        return session.AccessTokenExpiresAtUtc - _timeProvider.GetUtcNow()
            <= TimeSpan.FromMinutes(_options.RefreshThresholdMinutes);
    }

    private async Task<BffRefreshOutcome> RefreshAsLeaderAsync(
        string sessionId,
        CancellationToken ct
    )
    {
        BffSessionRecord? currentSession = await _sessionStore.GetAsync(sessionId, ct);
        if (currentSession is null)
            return BffRefreshOutcome.Failed(BffSessionRevocationReason.SessionCorrupted);

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
                await _revocationService.RevokeAsync(sessionId, reason, ct);

            return BffRefreshOutcome.Failed(reason);
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        BffSessionRecord updatedSession = currentSession with
        {
            AccessToken = refreshResult.TokenResponse.AccessToken,
            RefreshToken = refreshResult.TokenResponse.RefreshToken ?? currentSession.RefreshToken,
            AccessTokenExpiresAtUtc = now.AddSeconds(refreshResult.TokenResponse.ExpiresIn),
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
            return await ReloadFollowerOutcomeAsync(sessionId, ct);

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
