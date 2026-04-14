using Identity.Auth.Options;
using Identity.Auth.Security;
using Identity.Auth.Security.Keycloak;
using Identity.Auth.Security.Sessions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

public sealed class BffTokenRefreshServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-04-11T10:00:00Z");

    private readonly Mock<IBffRefreshCoordinator> _refreshCoordinator = new();
    private readonly Mock<IBffSessionStore> _sessionStore = new();
    private readonly Mock<IKeycloakService> _keycloakService = new();

    [Fact]
    public async Task RefreshIfRequired_WhenNotRequired_ReturnsNotRequired()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffSessionRecord freshSession = CreateSession() with
        {
            AccessTokenExpiresAtUtc = Now.AddMinutes(15),
        };

        BffTokenRefreshService sut = CreateSut();

        BffRefreshOutcome result = await sut.RefreshIfRequiredAsync(freshSession, ct);

        result.Succeeded.ShouldBeTrue();
        result.RequiresRenewal.ShouldBeFalse();
    }

    [Fact]
    public async Task RefreshIfRequired_WhenLeaderReloadsFreshSession_DoesNotRefreshAgain()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffSessionRecord staleSession = CreateSession() with
        {
            AccessToken = "stale-access",
            RefreshToken = "stale-refresh",
            AccessTokenExpiresAtUtc = Now.AddMinutes(1),
            Version = 1,
        };
        BffSessionRecord refreshedSession = staleSession with
        {
            AccessToken = "fresh-access",
            RefreshToken = "fresh-refresh",
            AccessTokenExpiresAtUtc = Now.AddMinutes(15),
            RefreshTokenExpiresAtUtc = Now.AddDays(30),
            LastRefreshedAtUtc = Now,
            Version = 2,
        };

        _sessionStore
            .Setup(x => x.GetAsync(staleSession.SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(refreshedSession);
        _refreshCoordinator
            .Setup(x =>
                x.ExecuteAsync(
                    staleSession.SessionId,
                    It.IsAny<Func<CancellationToken, Task<BffRefreshOutcome>>>(),
                    It.IsAny<Func<CancellationToken, Task<BffRefreshOutcome>>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<
                string,
                Func<CancellationToken, Task<BffRefreshOutcome>>,
                Func<CancellationToken, Task<BffRefreshOutcome>>,
                CancellationToken
            >((_, leaderAction, _, token) => leaderAction(token));

        BffTokenRefreshService sut = CreateSut();

        BffRefreshOutcome result = await sut.RefreshIfRequiredAsync(staleSession, ct);

        result.Succeeded.ShouldBeTrue();
        result.RequiresRenewal.ShouldBeFalse();
        result.Session.ShouldBe(refreshedSession);
        _keycloakService.Verify(
            x => x.RefreshSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task RefreshIfRequired_WhenRefreshFailureLosesRevocationRace_ReloadsUpdatedSession()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffSessionRecord expiringSession = CreateSession() with
        {
            AccessTokenExpiresAtUtc = Now.AddMinutes(1),
            RefreshToken = "refresh-race",
            Version = 4,
        };
        BffSessionRecord refreshedSession = expiringSession with
        {
            AccessToken = "fresh-access",
            RefreshToken = "fresh-refresh",
            AccessTokenExpiresAtUtc = Now.AddMinutes(15),
            RefreshTokenExpiresAtUtc = Now.AddDays(30),
            LastRefreshedAtUtc = Now,
            Version = 5,
        };

        _sessionStore
            .SetupSequence(x =>
                x.GetAsync(expiringSession.SessionId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(expiringSession)
            .ReturnsAsync(refreshedSession);
        _sessionStore
            .Setup(x =>
                x.TryUpdateAsync(
                    It.Is<BffSessionRecord>(session =>
                        session.SessionId == expiringSession.SessionId
                        && session.Status == BffSessionStatus.Revoked
                        && session.RevocationReason == BffSessionRevocationReason.RefreshRejected
                    ),
                    expiringSession.Version,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(false);
        _keycloakService
            .Setup(x =>
                x.RefreshSessionAsync(expiringSession.RefreshToken, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new KeycloakRefreshResult(KeycloakRefreshStatus.Rejected));
        _refreshCoordinator
            .Setup(x =>
                x.ExecuteAsync(
                    expiringSession.SessionId,
                    It.IsAny<Func<CancellationToken, Task<BffRefreshOutcome>>>(),
                    It.IsAny<Func<CancellationToken, Task<BffRefreshOutcome>>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<
                string,
                Func<CancellationToken, Task<BffRefreshOutcome>>,
                Func<CancellationToken, Task<BffRefreshOutcome>>,
                CancellationToken
            >((_, leaderAction, _, token) => leaderAction(token));

        BffTokenRefreshService sut = CreateSut();

        BffRefreshOutcome result = await sut.RefreshIfRequiredAsync(expiringSession, ct);

        result.Succeeded.ShouldBeTrue();
        _sessionStore.Verify(
            x =>
                x.TryUpdateAsync(
                    It.Is<BffSessionRecord>(session =>
                        session.SessionId == expiringSession.SessionId
                        && session.Status == BffSessionStatus.Revoked
                    ),
                    expiringSession.Version,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    private BffTokenRefreshService CreateSut()
    {
        return new BffTokenRefreshService(
            _refreshCoordinator.Object,
            _sessionStore.Object,
            _keycloakService.Object,
            Options.Create(new BffOptions { RefreshThresholdMinutes = 2 }),
            new FakeTimeProvider(Now)
        );
    }

    private static BffSessionRecord CreateSession()
    {
        return new BffSessionRecord
        {
            SessionId = "session-1",
            UserId = "user-1",
            Subject = "sub-1",
            Provider = BffProviderType.Keycloak,
            AccessToken = "access-1",
            RefreshToken = "refresh-1",
            AccessTokenExpiresAtUtc = Now.AddMinutes(5),
            CreatedAtUtc = Now.AddMinutes(-30),
            LastSeenAtUtc = Now.AddMinutes(-1),
            LastRefreshedAtUtc = Now.AddMinutes(-5),
            Status = BffSessionStatus.Active,
            Version = 1,
        };
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
