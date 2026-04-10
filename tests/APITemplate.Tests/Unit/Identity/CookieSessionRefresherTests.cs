using System.Security.Claims;
using Identity.Security;
using Identity.Security.Sessions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

public sealed class CookieSessionRefresherTests
{
    private readonly Mock<IBffSessionService> _sessionService = new();
    private readonly Mock<IBffSessionPrincipalFactory> _principalFactory = new();
    private readonly Mock<IBffTokenRefreshService> _refreshService = new();

    private CookieSessionRefresher CreateSut()
    {
        return new CookieSessionRefresher(
            _sessionService.Object,
            _principalFactory.Object,
            _refreshService.Object,
            NullLogger<CookieSessionRefresher>.Instance
        );
    }

    [Fact]
    public async Task ValidatePrincipal_WhenRefreshNotRequired_DoesNothing()
    {
        CookieSessionRefresher sut = CreateSut();
        CookieValidatePrincipalContext context = CreateContext(sessionId: "session-1");
        BffSessionRecord session = CreateSession("access-1", "refresh-1");

        _sessionService
            .Setup(x => x.GetSessionAsync("session-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _refreshService
            .Setup(x =>
                x.RefreshIfRequiredAsync(
                    It.Is<BffSessionRecord>(s => s.SessionId == "session-1"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(BffRefreshOutcome.NotRequired(session));

        await sut.ValidatePrincipal(context);

        context.ShouldRenew.ShouldBeFalse();
        context.Principal.ShouldNotBeNull();
    }

    [Fact]
    public async Task ValidatePrincipal_WhenRefreshSucceeds_RenewsCookieAndUpdatesTokens()
    {
        CookieSessionRefresher sut = CreateSut();
        CookieValidatePrincipalContext context = CreateContext(sessionId: "session-1");
        BffSessionRecord session = CreateSession("access-1", "refresh-1");
        BffSessionRecord refreshedSession = CreateSession("new-access", "new-refresh");
        ClaimsPrincipal refreshedPrincipal = new(
            new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, refreshedSession.UserId)],
                AuthConstants.BffSchemes.Cookie
            )
        );

        _sessionService
            .Setup(x => x.GetSessionAsync("session-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _refreshService
            .Setup(x =>
                x.RefreshIfRequiredAsync(
                    It.Is<BffSessionRecord>(s => s.SessionId == "session-1"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(BffRefreshOutcome.Success(refreshedSession));
        _principalFactory
            .Setup(x => x.CreatePrincipal(refreshedSession))
            .Returns(refreshedPrincipal);

        await sut.ValidatePrincipal(context);

        context.ShouldRenew.ShouldBeTrue();
        context.Principal.ShouldBe(refreshedPrincipal);
        context
            .Properties.GetTokenValue(AuthConstants.CookieTokenNames.AccessToken)
            .ShouldBe("new-access");
        context
            .Properties.GetTokenValue(AuthConstants.CookieTokenNames.RefreshToken)
            .ShouldBe("new-refresh");
    }

    [Fact]
    public async Task ValidatePrincipal_WhenRefreshFails_RejectsPrincipal()
    {
        CookieSessionRefresher sut = CreateSut();
        CookieValidatePrincipalContext context = CreateContext(sessionId: "session-1");
        BffSessionRecord session = CreateSession("access-1", "refresh-1");

        _sessionService
            .Setup(x => x.GetSessionAsync("session-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _refreshService
            .Setup(x =>
                x.RefreshIfRequiredAsync(
                    It.Is<BffSessionRecord>(s => s.SessionId == "session-1"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(BffRefreshOutcome.Failed(BffSessionRevocationReason.RefreshRejected));

        await sut.ValidatePrincipal(context);

        context.Principal.ShouldBeNull();
        context.ShouldRenew.ShouldBeFalse();
    }

    [Fact]
    public async Task ValidatePrincipal_WhenSessionNotFound_RejectsPrincipal()
    {
        CookieSessionRefresher sut = CreateSut();
        CookieValidatePrincipalContext context = CreateContext(sessionId: "session-1");

        _sessionService
            .Setup(x => x.GetSessionAsync("session-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((BffSessionRecord?)null);

        await sut.ValidatePrincipal(context);

        context.Principal.ShouldBeNull();
    }

    [Fact]
    public async Task ValidatePrincipal_WhenSessionCookieMissing_RejectsPrincipal()
    {
        CookieSessionRefresher sut = CreateSut();
        CookieValidatePrincipalContext context = CreateContext(sessionId: null);

        await sut.ValidatePrincipal(context);

        context.Principal.ShouldBeNull();
    }

    private static CookieValidatePrincipalContext CreateContext(string? sessionId)
    {
        DefaultHttpContext httpContext = new();

        ClaimsPrincipal principal = new(
            new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
                "cookie"
            )
        );
        AuthenticationProperties properties = new();
        if (sessionId is not null)
            properties.Items[AuthConstants.SessionProperties.SessionId] = sessionId;

        properties.StoreTokens([
            new AuthenticationToken
            {
                Name = AuthConstants.CookieTokenNames.AccessToken,
                Value = "old-access",
            },
            new AuthenticationToken
            {
                Name = AuthConstants.CookieTokenNames.RefreshToken,
                Value = "old-refresh",
            },
            new AuthenticationToken
            {
                Name = AuthConstants.CookieTokenNames.ExpiresAt,
                Value = "2026-04-10T10:05:00+00:00",
            },
        ]);

        AuthenticationTicket ticket = new(principal, properties, AuthConstants.BffSchemes.Cookie);
        AuthenticationScheme scheme = new(
            AuthConstants.BffSchemes.Cookie,
            AuthConstants.BffSchemes.Cookie,
            typeof(CookieAuthenticationHandler)
        );

        return new CookieValidatePrincipalContext(
            httpContext,
            scheme,
            new CookieAuthenticationOptions(),
            ticket
        );
    }

    private static BffSessionRecord CreateSession(string accessToken, string refreshToken)
    {
        return new BffSessionRecord
        {
            SessionId = "session-1",
            UserId = Guid.NewGuid().ToString(),
            Subject = Guid.NewGuid().ToString(),
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessTokenExpiresAtUtc = DateTimeOffset.Parse("2026-04-10T10:05:00Z"),
            CreatedAtUtc = DateTimeOffset.Parse("2026-04-10T10:00:00Z"),
            LastSeenAtUtc = DateTimeOffset.Parse("2026-04-10T10:00:00Z"),
            LastRefreshedAtUtc = DateTimeOffset.Parse("2026-04-10T10:00:00Z"),
            Version = 1,
        };
    }
}
