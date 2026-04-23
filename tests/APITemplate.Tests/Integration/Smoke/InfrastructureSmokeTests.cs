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

    public InfrastructureSmokeTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Root_RequiresAuthentication()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        HttpResponseMessage response = await Client.GetAsync("/", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Liveness_ReturnsOk()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        HttpResponseMessage response = await Client.GetAsync("/health/live", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Readiness_ReturnsOk()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        HttpResponseMessage response = await Client.GetAsync("/health/ready", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ComprehensiveHealth_ReturnsOk()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        HttpResponseMessage response = await Client.GetAsync("/health", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OpenApiSpec_ReturnsJsonDocument()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        HttpResponseMessage response = await Client.GetAsync("/openapi/v1.json", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        string body = await response.Content.ReadAsStringAsync(ct);
        body.ShouldContain("\"openapi\"");
    }

    [Fact]
    public async Task BffExternalProviders_ReturnsOk()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        HttpResponseMessage response = await Client.GetAsync("/api/v1/bff/external-providers", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
