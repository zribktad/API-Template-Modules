using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Smoke;

[Collection("Smoke")]
[Trait("Category", "Smoke")]
public sealed class InfrastructureSmokeTests
{
    private readonly CustomWebApplicationFactory _factory;
    private HttpClient? _client;

    private HttpClient Client =>
        _client ??= _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

    public InfrastructureSmokeTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Root_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await Client.GetAsync("/", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
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
    public async Task ComprehensiveHealth_ReturnsOkAndHealthyStatus()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await Client.GetAsync("/health", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(ct);
        using JsonDocument doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("status").GetString().ShouldBe("Healthy");
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

    [Fact]
    public async Task BffLogin_ReturnsRedirect()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await Client.GetAsync("/api/v1/bff/login", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    }
}
