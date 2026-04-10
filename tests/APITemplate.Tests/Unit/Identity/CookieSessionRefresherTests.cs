using System.Security.Claims;
using APITemplate.Tests.Unit.Helpers;
using Identity.Options;
using Identity.Security;
using Identity.Security.Keycloak;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

public sealed class CookieSessionRefresherTests
{
    private static readonly DateTimeOffset FixedNow = DateTimeOffset.Parse("2026-04-10T10:00:00Z");

    private readonly Mock<IKeycloakService> _keycloakService = new();
    private readonly FakeTimeProvider _timeProvider = new(FixedNow);

    private CookieSessionRefresher CreateSut(int refreshThresholdMinutes = 2)
    {
        return new CookieSessionRefresher(
            Options.Create(new BffOptions { TokenRefreshThresholdMinutes = refreshThresholdMinutes }),
            _keycloakService.Object,
            NullLogger<CookieSessionRefresher>.Instance,
            _timeProvider
        );
    }

    [Fact]
    public async Task ValidatePrincipal_WhenRefreshNotRequired_DoesNothing()
    {
        CookieSessionRefresher sut = CreateSut();
        CookieValidatePrincipalContext context = CreateContext(
            expiresAt: FixedNow.AddMinutes(10).ToString("o"),
            refreshToken: "refresh-token"
        );

        await sut.ValidatePrincipal(context);

        context.ShouldRenew.ShouldBeFalse();
        context.Principal.ShouldNotBeNull();
        _keycloakService.Verify(
            x => x.RefreshSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ValidatePrincipal_WhenRefreshSucceeds_RenewsCookieAndUpdatesTokens()
    {
        CookieSessionRefresher sut = CreateSut();
        CookieValidatePrincipalContext context = CreateContext(
            expiresAt: FixedNow.AddMinutes(1).ToString("o"),
            refreshToken: "old-refresh",
            accessToken: "old-access"
        );
        _keycloakService
            .Setup(x => x.RefreshSessionAsync("old-refresh", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KeycloakTokenResponse("new-access", "new-refresh", 300));

        await sut.ValidatePrincipal(context);

        context.ShouldRenew.ShouldBeTrue();
        context.Properties.GetTokenValue(AuthConstants.CookieTokenNames.AccessToken).ShouldBe(
            "new-access"
        );
        context.Properties.GetTokenValue(AuthConstants.CookieTokenNames.RefreshToken).ShouldBe(
            "new-refresh"
        );
        context.Properties.GetTokenValue(AuthConstants.CookieTokenNames.ExpiresAt).ShouldBe(
            FixedNow.AddSeconds(300).ToString("o")
        );
    }

    [Fact]
    public async Task ValidatePrincipal_WhenExpiresAtMissing_RejectsPrincipal()
    {
        CookieSessionRefresher sut = CreateSut();
        CookieValidatePrincipalContext context = CreateContext(
            expiresAt: null,
            refreshToken: "refresh-token"
        );

        await sut.ValidatePrincipal(context);

        context.Principal.ShouldBeNull();
        _keycloakService.Verify(
            x => x.RefreshSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ValidatePrincipal_WhenRefreshTokenMissing_RejectsPrincipal()
    {
        CookieSessionRefresher sut = CreateSut();
        CookieValidatePrincipalContext context = CreateContext(
            expiresAt: FixedNow.AddMinutes(1).ToString("o"),
            refreshToken: null
        );

        await sut.ValidatePrincipal(context);

        context.Principal.ShouldBeNull();
        _keycloakService.Verify(
            x => x.RefreshSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ValidatePrincipal_WhenRefreshReturnsNull_RejectsPrincipal()
    {
        CookieSessionRefresher sut = CreateSut();
        CookieValidatePrincipalContext context = CreateContext(
            expiresAt: FixedNow.AddMinutes(1).ToString("o"),
            refreshToken: "refresh-token"
        );
        _keycloakService
            .Setup(x => x.RefreshSessionAsync("refresh-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync((KeycloakTokenResponse?)null);

        await sut.ValidatePrincipal(context);

        context.Principal.ShouldBeNull();
        context.ShouldRenew.ShouldBeFalse();
    }

    [Fact]
    public async Task ValidatePrincipal_WhenRequestIsCanceled_PropagatesOperationCanceled()
    {
        CookieSessionRefresher sut = CreateSut();
        CancellationTokenSource cts = new();
        CookieValidatePrincipalContext context = CreateContext(
            expiresAt: FixedNow.AddMinutes(1).ToString("o"),
            refreshToken: "refresh-token"
        );
        context.HttpContext.RequestAborted = cts.Token;
        cts.Cancel();
        _keycloakService
            .Setup(x => x.RefreshSessionAsync("refresh-token", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(context.HttpContext.RequestAborted));

        await Should.ThrowAsync<OperationCanceledException>(() => sut.ValidatePrincipal(context));
    }

    private static CookieValidatePrincipalContext CreateContext(
        string? expiresAt,
        string? refreshToken,
        string? accessToken = null
    )
    {
        DefaultHttpContext httpContext = new();
        ClaimsPrincipal principal = new(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "cookie")
        );
        AuthenticationProperties properties = new();

        List<AuthenticationToken> tokens = [];
        if (expiresAt is not null)
            tokens.Add(new AuthenticationToken { Name = AuthConstants.CookieTokenNames.ExpiresAt, Value = expiresAt });
        if (refreshToken is not null)
            tokens.Add(new AuthenticationToken { Name = AuthConstants.CookieTokenNames.RefreshToken, Value = refreshToken });
        if (accessToken is not null)
            tokens.Add(new AuthenticationToken { Name = AuthConstants.CookieTokenNames.AccessToken, Value = accessToken });

        properties.StoreTokens(tokens);

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
}
