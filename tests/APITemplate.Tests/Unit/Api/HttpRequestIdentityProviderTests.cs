using System.Security.Claims;
using APITemplate.Api.Security;
using Identity.Auth.Security;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Api;

[Trait("Category", "Unit")]
public sealed class HttpRequestIdentityProviderTests
{
    private static HttpRequestIdentityProvider CreateProvider(ClaimsPrincipal principal)
    {
        DefaultHttpContext httpContext = new() { User = principal };
        IHttpContextAccessor accessor = new TestHttpContextAccessor(httpContext);
        return new HttpRequestIdentityProvider(accessor);
    }

    [Fact]
    public void BffSession_DistinctNameIdentifierAndSubject_SetsApplicationUserIdAndOidcSubject()
    {
        Guid appId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        string kcSub = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
        ClaimsPrincipal principal = CreatePrincipal([
            new Claim(ClaimTypes.NameIdentifier, appId.ToString()),
            new Claim(AuthConstants.Claims.Subject, kcSub),
            new Claim(AuthConstants.Claims.PreferredUsername, "alice"),
        ]);

        HttpRequestIdentityProvider sut = CreateProvider(principal);

        sut.ApplicationUserId.ShouldBe(appId);
        sut.OidcSubject.ShouldBe(kcSub);
        sut.PreferredUsername.ShouldBe("alice");
        sut.IsInteractiveUser.ShouldBeTrue();
        sut.ActorId.ShouldBe(appId);
    }

    [Fact]
    public void JwtStyle_NameIdentifierEqualsSub_DoesNotSetApplicationUserId_UsesOidcSubject()
    {
        string kcSub = "cccccccc-cccc-cccc-cccc-cccccccccccc";
        ClaimsPrincipal principal = CreatePrincipal([
            new Claim(ClaimTypes.NameIdentifier, kcSub),
            new Claim(AuthConstants.Claims.Subject, kcSub),
            new Claim(AuthConstants.Claims.PreferredUsername, "bob"),
        ]);

        HttpRequestIdentityProvider sut = CreateProvider(principal);

        sut.ApplicationUserId.ShouldBeNull();
        sut.OidcSubject.ShouldBe(kcSub);
        sut.ActorId.ShouldBe(Guid.Parse(kcSub));
    }

    [Fact]
    public void ServiceAccount_IsInteractiveUserFalse()
    {
        ClaimsPrincipal principal = CreatePrincipal([
            new Claim(AuthConstants.Claims.Subject, Guid.NewGuid().ToString()),
            new Claim(
                AuthConstants.Claims.PreferredUsername,
                AuthConstants.Claims.ServiceAccountUsernamePrefix + "my-api"
            ),
        ]);

        HttpRequestIdentityProvider sut = CreateProvider(principal);

        sut.IsInteractiveUser.ShouldBeFalse();
    }

    [Fact]
    public void ServiceAccount_WithAzpAndServiceSubject_NoPreferredUsername_IsInteractiveUserFalse()
    {
        string serviceSub =
            AuthConstants.Claims.ServiceAccountUsernamePrefix
            + "00000000-0000-0000-0000-000000000000";
        ClaimsPrincipal principal = CreatePrincipal([
            new Claim(AuthConstants.Claims.AuthorizedParty, "my-client-id"),
            new Claim(ClaimTypes.NameIdentifier, serviceSub),
        ]);

        HttpRequestIdentityProvider sut = CreateProvider(principal);

        sut.IsInteractiveUser.ShouldBeFalse();
    }

    [Fact]
    public void NoHttpContext_ActorIdEmptyAndNoSubject()
    {
        IHttpContextAccessor accessor = new TestHttpContextAccessor(null!);
        HttpRequestIdentityProvider sut = new(accessor);

        sut.ActorId.ShouldBe(Guid.Empty);
        sut.OidcSubject.ShouldBeNull();
        sut.ApplicationUserId.ShouldBeNull();
    }

    private static ClaimsPrincipal CreatePrincipal(IEnumerable<Claim> claims)
    {
        ClaimsIdentity identity = new(claims, authenticationType: "Test");
        return new ClaimsPrincipal(identity);
    }

    private sealed class TestHttpContextAccessor(HttpContext? context) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = context;
    }
}
