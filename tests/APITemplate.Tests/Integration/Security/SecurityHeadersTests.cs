using System.Net;
using APITemplate.Tests.Integration.Helpers;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Security;

[Trait("Category", "Integration")]
[Trait("Category", "Integration.Docker")]
[Trait("Docker", "true")]
public sealed class SecurityHeadersTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SecurityHeadersTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RequestToApiEndpoint_ShouldContainSecurityHeaders()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health/ready", ct);

        // Assert
        response.Headers.Contains("X-Frame-Options").ShouldBeTrue();
        response.Headers.GetValues("X-Frame-Options").ShouldContain("DENY");

        response.Headers.Contains("X-Content-Type-Options").ShouldBeTrue();
        response.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");

        response.Headers.Contains("Content-Security-Policy").ShouldBeTrue();
        // Check that CSP contains default-src 'none'
        response
            .Headers.GetValues("Content-Security-Policy")
            .First()
            .ShouldContain("default-src 'none'");

        response.Headers.Contains("Referrer-Policy").ShouldBeTrue();
        response
            .Headers.GetValues("Referrer-Policy")
            .ShouldContain("strict-origin-when-cross-origin");

        response.Headers.Contains("Permissions-Policy").ShouldBeTrue();
    }
}
