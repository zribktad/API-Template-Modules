using System.Security.Claims;
using Identity.Auth.Options;
using Identity.Auth.Security;
using Identity.Auth.Security.Sessions;
using Identity.Auth.Security.Sessions.Lifecycle;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

[Trait("Category", "Unit")]
public sealed class BffSessionServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-04-11T10:00:00Z");

    private readonly Mock<IBffSessionStore> _sessionStore = new();
    private readonly Mock<IBffSessionPrincipalFactory> _principalFactory = new();

    [Fact]
    public async Task CreateSessionAsync_StoresNewSessionAndReturnsId()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AuthenticationTicket ticket = CreateTicket("access-1", "refresh-1");

        BffSessionService sut = CreateSut();

        string sessionId = await sut.CreateSessionAsync(ticket, ct);

        sessionId.ShouldNotBeNullOrWhiteSpace();
        _sessionStore.Verify(
            x => x.StoreAsync(It.Is<BffSessionRecord>(s => s.SessionId == sessionId), ct),
            Times.Once
        );
    }

    [Fact]
    public async Task GetSessionAsync_WhenSessionIsRevoked_ReturnsNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffSessionRecord revokedSession = CreateSession() with
        {
            Status = BffSessionStatus.Revoked,
        };

        _sessionStore
            .Setup(x => x.GetAsync(revokedSession.SessionId, ct))
            .ReturnsAsync(revokedSession);

        BffSessionService sut = CreateSut();

        BffSessionRecord? result = await sut.GetSessionAsync(revokedSession.SessionId, ct);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateSessionFromTicketAsync_PreservesRefreshTokenExpiry()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffSessionRecord currentSession = CreateSession() with
        {
            RefreshTokenExpiresAtUtc = Now.AddDays(30),
            Version = 3,
        };
        AuthenticationTicket updatedTicket = CreateTicket(
            accessToken: "new-access",
            refreshToken: "new-refresh"
        );

        BffSessionRecord? capturedUpdate = null;
        _sessionStore
            .Setup(x => x.GetAsync(currentSession.SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentSession);
        _sessionStore
            .Setup(x =>
                x.TryUpdateAsync(
                    It.IsAny<BffSessionRecord>(),
                    currentSession.Version,
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<BffSessionRecord, long, CancellationToken>(
                (updatedSession, _, _) => capturedUpdate = updatedSession
            )
            .ReturnsAsync(true);

        BffSessionService sut = CreateSut();

        await sut.UpdateSessionFromTicketAsync(currentSession.SessionId, updatedTicket, ct);

        capturedUpdate.ShouldNotBeNull();
        capturedUpdate.AccessToken.ShouldBe("new-access");
        capturedUpdate.RefreshToken.ShouldBe("new-refresh");
        capturedUpdate.RefreshTokenExpiresAtUtc.ShouldBe(currentSession.RefreshTokenExpiresAtUtc);
        capturedUpdate.Version.ShouldBe(currentSession.Version + 1);
    }

    [Fact]
    public async Task GetSessionAsync_WhenMalformed_RevokesAndReturnsNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffSessionRecord malformed = CreateSession() with { AccessToken = "" };

        _sessionStore.Setup(x => x.GetAsync(malformed.SessionId, ct)).ReturnsAsync(malformed);
        _sessionStore
            .Setup(x => x.GetAsync(malformed.SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(malformed);
        _sessionStore
            .Setup(x =>
                x.TryUpdateAsync(
                    It.IsAny<BffSessionRecord>(),
                    malformed.Version,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(true);

        BffSessionService sut = CreateSut();

        BffSessionRecord? result = await sut.GetSessionAsync(malformed.SessionId, ct);

        result.ShouldBeNull();
    }

    private BffSessionService CreateSut()
    {
        BffSessionValidator validator = new(Options.Create(new BffOptions()));
        BffSessionMutator mutator = new(
            _sessionStore.Object,
            new Mock<IHttpContextAccessor>().Object,
            NullLogger<BffSessionMutator>.Instance
        );

        return new BffSessionService(
            _sessionStore.Object,
            _principalFactory.Object,
            new BffSessionRecordFactory(),
            validator,
            mutator,
            new FakeTimeProvider(Now),
            new Mock<IHttpContextAccessor>().Object
        );
    }

    private static AuthenticationTicket CreateTicket(string accessToken, string refreshToken)
    {
        ClaimsPrincipal principal = new(
            new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "user-1"),
                    new Claim(AuthConstants.Claims.Subject, "sub-1"),
                    new Claim(ClaimTypes.Email, "user@example.com"),
                    new Claim(ClaimTypes.Name, "Test User"),
                    new Claim(ClaimTypes.Role, "Admin"),
                ],
                AuthConstants.BffSchemes.Cookie
            )
        );

        AuthenticationProperties properties = new();
        properties.StoreTokens([
            new AuthenticationToken
            {
                Name = AuthConstants.CookieTokenNames.AccessToken,
                Value = accessToken,
            },
            new AuthenticationToken
            {
                Name = AuthConstants.CookieTokenNames.RefreshToken,
                Value = refreshToken,
            },
            new AuthenticationToken
            {
                Name = AuthConstants.CookieTokenNames.ExpiresAt,
                Value = Now.AddMinutes(5).ToString("o"),
            },
        ]);

        return new AuthenticationTicket(principal, properties, AuthConstants.BffSchemes.Cookie);
    }

    private static BffSessionRecord CreateSession()
    {
        return new BffSessionRecord
        {
            SessionId = "session-1",
            UserId = "user-1",
            Subject = "sub-1",
            Provider = BffProviderType.Keycloak,
            Roles = ["Admin"],
            Email = "user@example.com",
            DisplayName = "Test User",
            AccessToken = "access-1",
            RefreshToken = "refresh-1",
            AccessTokenExpiresAtUtc = Now.AddMinutes(1),
            CreatedAtUtc = Now.AddMinutes(-10),
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
