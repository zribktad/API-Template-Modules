using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Infrastructure;

public class CorsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CorsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Preflight_FromAllowedOrigin_ReturnsCorsHeaders()
    {
        var ct = TestContext.Current.CancellationToken;
        using var request = new HttpRequestMessage(HttpMethod.Options, "/graphql");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await _client.SendAsync(request, ct);

        response.IsSuccessStatusCode.ShouldBeTrue();
        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowedOrigins).ShouldBeTrue();
        allowedOrigins!.Single().ShouldBe("http://localhost:3000");

        response.Headers.TryGetValues("Access-Control-Allow-Credentials", out var credValues).ShouldBeTrue();
        credValues!.Single().ShouldBe("true");
    }

    [Fact]
    public async Task Preflight_FromDisallowedOrigin_DoesNotReturnAllowOriginHeader()
    {
        var ct = TestContext.Current.CancellationToken;
        using var request = new HttpRequestMessage(HttpMethod.Options, "/graphql");
        request.Headers.Add("Origin", "http://evil.example");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await _client.SendAsync(request, ct);

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    [Fact]
    public async Task Get_AnonymousEndpoint_FromAllowedOrigin_ReturnsCorsHeaders()
    {
        var ct = TestContext.Current.CancellationToken;
        using var request = new HttpRequestMessage(HttpMethod.Get, "/openapi/v1.json");
        request.Headers.Add("Origin", "http://localhost:3000");

        var response = await _client.SendAsync(request, ct);

        response.IsSuccessStatusCode.ShouldBeTrue();
        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowedOrigins).ShouldBeTrue();
        allowedOrigins!.Single().ShouldBe("http://localhost:3000");
        response.Headers.TryGetValues("Access-Control-Allow-Credentials", out var credValues).ShouldBeTrue();
        credValues!.Single().ShouldBe("true");
    }

    [Fact]
    public async Task Get_ProtectedEndpoint_FromAllowedOrigin_ReturnsCorsHeadersEvenWithout401()
    {
        var ct = TestContext.Current.CancellationToken;
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/products");
        request.Headers.Add("Origin", "http://localhost:3000");

        var response = await _client.SendAsync(request, ct);

        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowedOrigins).ShouldBeTrue();
        allowedOrigins!.Single().ShouldBe("http://localhost:3000");
        response.Headers.TryGetValues("Access-Control-Allow-Credentials", out var credValues).ShouldBeTrue();
        credValues!.Single().ShouldBe("true");
    }
}
