using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Smoke;

[Collection("Smoke")]
[Trait("Category", "Smoke")]
public sealed class InfrastructureSmokeTests
{
    private readonly CustomWebApplicationFactory _factory;

    private HttpClient Client =>
        field ??= _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

    public InfrastructureSmokeTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Root_RequiresAuthentication()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await Client.GetAsync("/", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Liveness_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await Client.GetAsync("/health/live", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Readiness_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await Client.GetAsync("/health/ready", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ComprehensiveHealth_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await Client.GetAsync("/health", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OpenApiSpec_ReturnsJsonDocument()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await Client.GetAsync("/openapi/v1.json", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(ct);
        body.ShouldContain("\"openapi\"");
    }

    [Fact]
    public async Task BffExternalProviders_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await Client.GetAsync("/api/v1/bff/external-providers", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
