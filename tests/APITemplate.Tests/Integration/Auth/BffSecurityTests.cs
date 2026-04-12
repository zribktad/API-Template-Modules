using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using APITemplate.Tests.Integration.Helpers;
using Identity.Auth.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Auth;

[Trait("Category", "Integration.Docker")]
public sealed class BffSecurityTests : IClassFixture<BffSecurityWebApplicationFactory>
{
    private readonly BffSecurityWebApplicationFactory _factory;

    public BffSecurityTests(BffSecurityWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostWithCookieAuth_WithoutCsrfHeader_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Cookie-Auth", "1");

        var response = await client.PostAsync(
            "/api/v1/products",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
            ct
        );

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostWithCookieAuth_WithCsrfHeader_PassesCsrfCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Cookie-Auth", "1");
        string csrf = await GetCsrfHeaderValueAsync(client, ct);
        client.DefaultRequestHeaders.Add(AuthConstants.Csrf.HeaderName, csrf);

        var response = await client.PostAsync(
            "/api/v1/products",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
            ct
        );

        // CSRF passes; request reaches the controller where the empty body fails validation.
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostWithJwtBearer_WithoutCsrfHeader_PassesCsrfCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        // Service-account principal skips DB user resolution (IdentityTokenValidatedPipeline); a human test
        // token is denied without invitation data and can leave the response in a bad state for Challenge.
        IntegrationAuthHelper.Authenticate(
            client,
            username: $"{AuthConstants.Claims.ServiceAccountUsernamePrefix}bff-security"
        );

        var response = await client.PostAsync(
            "/api/v1/products",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
            ct
        );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetCsrf_WithoutBffSession_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/bff/csrf", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCsrf_WithFakeCookieAuth_ReturnsDataProtectionToken()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Cookie-Auth", "1");

        var response = await client.GetAsync("/api/v1/bff/csrf", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(ct);
        body.ShouldContain("X-CSRF");
        body.ShouldContain("headerName");
        body.ShouldContain("headerValue");
        body.ShouldContain(AuthConstants.Csrf.TokenFormats.DataProtection);
    }

    [Fact]
    public async Task LogoutWithCookieAuth_WithoutCsrfHeader_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Cookie-Auth", "1");

        var response = await client.GetAsync(AuthConstants.BffRoutes.Logout, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task LogoutWithCookieAuth_WithCsrfHeader_DoesNotReturn403()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Cookie-Auth", "1");
        string csrf = await GetCsrfHeaderValueAsync(client, ct);
        client.DefaultRequestHeaders.Add(AuthConstants.Csrf.HeaderName, csrf);

        var response = await client.GetAsync(AuthConstants.BffRoutes.Logout, ct);

        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);
    }

    private static async Task<string> GetCsrfHeaderValueAsync(
        HttpClient client,
        CancellationToken ct
    )
    {
        using var response = await client.GetAsync("/api/v1/bff/csrf", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return doc.RootElement.GetProperty("headerValue").GetString()!;
    }
}

/// <summary>
/// Extends <see cref="CustomWebApplicationFactory"/> by replacing the real BFF Cookie
/// authentication handler with <see cref="FakeCookieAuthHandler"/>. When the
/// <c>X-Test-Cookie-Auth: 1</c> request header is present, the handler returns
/// an authenticated principal so that authorization policies that explicitly list the
/// <c>BffCookie</c> scheme authenticate correctly.
/// </summary>
public sealed class BffSecurityWebApplicationFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            // Replace the real BffCookie handler with the fake one by swapping the handler type
            // in the existing SchemeBuilder — avoids "scheme already exists" errors.
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                if (options.SchemeMap.TryGetValue(AuthConstants.BffSchemes.Cookie, out var builder))
                    builder.HandlerType = typeof(FakeCookieAuthHandler);
            });
        });
    }
}

/// <summary>
/// A fake authentication handler registered under the <c>BffCookie</c> scheme.
/// Returns <see cref="AuthenticateResult.Success"/> with a fabricated principal when
/// the <c>X-Test-Cookie-Auth: 1</c> request header is present; otherwise
/// <see cref="AuthenticateResult.NoResult"/>.
/// </summary>
internal sealed class FakeCookieAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("X-Test-Cookie-Auth"))
            return Task.FromResult(AuthenticateResult.NoResult());

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, "testuser"),
                new Claim(AuthConstants.Claims.Subject, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, "PlatformAdmin"),
                new Claim(
                    AuthConstants.Claims.Permission,
                    SharedKernel.Contracts.Security.Permission.Platform.Manage
                ),
            ],
            AuthConstants.BffSchemes.Cookie
        );

        var properties = new AuthenticationProperties();
        properties.Items[AuthConstants.SessionProperties.SessionId] =
            "bff-integration-test-session";

        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(identity),
            properties,
            AuthConstants.BffSchemes.Cookie
        );

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
