using Identity.Controllers.V1;
using Identity.Features.Bff.DTOs;
using Identity.Options;
using Identity.Security;
using Identity.Security.ExternalIdentityProviders;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

public sealed class BffExternalProvidersTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static BffController CreateController(
        IEnumerable<IExternalIdentityProvider> providers
    )
    {
        IOptions<BffOptions> options = Options.Create(new BffOptions());

        BffController controller = new(options, providers);

        Mock<IUrlHelper> urlHelper = new();
        urlHelper
            .Setup(u => u.IsLocalUrl(It.Is<string?>(s => s != null && s.StartsWith("/"))))
            .Returns(true);
        urlHelper
            .Setup(u => u.IsLocalUrl(It.Is<string?>(s => s == null || !s.StartsWith("/"))))
            .Returns(false);

        controller.Url = urlHelper.Object;
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };

        return controller;
    }

    // ── GetExternalProviders ─────────────────────────────────────────────────

    [Fact]
    public void GetExternalProviders_WithGoogleRegistered_ReturnsGoogleInList()
    {
        BffController controller = CreateController([new GoogleIdentityProvider()]);

        IActionResult result = controller.GetExternalProviders();

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        IEnumerable<ExternalProviderResponse> providers = ok.Value.ShouldBeAssignableTo<IEnumerable<ExternalProviderResponse>>()!;
        providers.ShouldNotBeEmpty();
    }

    [Fact]
    public void GetExternalProviders_WithNoProviders_ReturnsEmptyList()
    {
        BffController controller = CreateController([]);

        IActionResult result = controller.GetExternalProviders();

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        IEnumerable<ExternalProviderResponse> providers = ok.Value.ShouldBeAssignableTo<IEnumerable<ExternalProviderResponse>>()!;
        providers.ShouldBeEmpty();
    }

    [Fact]
    public void GetExternalProviders_WithMultipleProviders_ReturnsAll()
    {
        IExternalIdentityProvider[] twoProviders =
        [
            new GoogleIdentityProvider(),
            new StubIdentityProvider("github", "GitHub"),
        ];
        BffController controller = CreateController(twoProviders);

        IActionResult result = controller.GetExternalProviders();

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        ok.Value.ShouldBeAssignableTo<IEnumerable<ExternalProviderResponse>>()!.Count().ShouldBe(2);
    }

    // ── LoginWithProvider ────────────────────────────────────────────────────

    [Fact]
    public void LoginWithProvider_KnownHint_ReturnsChallengeResult()
    {
        BffController controller = CreateController([new GoogleIdentityProvider()]);

        IActionResult result = controller.LoginWithProvider("google");

        result.ShouldBeOfType<ChallengeResult>();
    }

    [Fact]
    public void LoginWithProvider_KnownHint_CaseInsensitive_ReturnsChallengeResult()
    {
        BffController controller = CreateController([new GoogleIdentityProvider()]);

        IActionResult result = controller.LoginWithProvider("GOOGLE");

        result.ShouldBeOfType<ChallengeResult>();
    }

    [Fact]
    public void LoginWithProvider_UnknownHint_Returns404()
    {
        BffController controller = CreateController([new GoogleIdentityProvider()]);

        IActionResult result = controller.LoginWithProvider("github");

        result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public void LoginWithProvider_EmptyProviderList_Returns404()
    {
        BffController controller = CreateController([]);

        IActionResult result = controller.LoginWithProvider("google");

        result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public void LoginWithProvider_LocalReturnUrl_IsForwardedToChallenge()
    {
        BffController controller = CreateController([new GoogleIdentityProvider()]);

        ChallengeResult result = controller
            .LoginWithProvider("google", "/dashboard")
            .ShouldBeOfType<ChallengeResult>();

        result.Properties!.RedirectUri.ShouldBe("/dashboard");
    }

    [Fact]
    public void LoginWithProvider_NonLocalReturnUrl_DefaultsToSlash()
    {
        BffController controller = CreateController([new GoogleIdentityProvider()]);

        ChallengeResult result = controller
            .LoginWithProvider("google", "https://evil.example.com")
            .ShouldBeOfType<ChallengeResult>();

        result.Properties!.RedirectUri.ShouldBe("/");
    }

    [Fact]
    public void LoginWithProvider_SetsIdpHintInAuthProperties()
    {
        BffController controller = CreateController([new GoogleIdentityProvider()]);

        ChallengeResult result = controller
            .LoginWithProvider("google")
            .ShouldBeOfType<ChallengeResult>();

        result.Properties!.Items.ShouldContainKey(AuthConstants.KeycloakAuthProperties.IdpHint);
        result.Properties.Items[AuthConstants.KeycloakAuthProperties.IdpHint].ShouldBe("google");
    }

    // ── GoogleIdentityProvider ───────────────────────────────────────────────

    [Fact]
    public void GoogleIdentityProvider_IdpHint_IsGoogle()
    {
        new GoogleIdentityProvider().IdpHint.ShouldBe("google");
    }

    [Fact]
    public void GoogleIdentityProvider_DisplayName_IsGoogle()
    {
        new GoogleIdentityProvider().DisplayName.ShouldBe("Google");
    }
}

/// <summary>Stub provider used in multi-provider tests.</summary>
file sealed class StubIdentityProvider(string idpHint, string displayName) : IExternalIdentityProvider
{
    public string IdpHint { get; } = idpHint;
    public string DisplayName { get; } = displayName;
}
