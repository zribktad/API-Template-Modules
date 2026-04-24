using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using APITemplate.Tests.Integration.Helpers;
using Identity.Directory.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using SharedKernel.Application.Http;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Infrastructure;

[Trait("Category", "Integration")]
[Trait("Docker", "true")]
public class ScalarAndOpenApiTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private Tenant _adminTenant = default!;
    private AppUser _adminUser = default!;

    public ScalarAndOpenApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
    }

    public async ValueTask InitializeAsync()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (_adminTenant, _adminUser) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            "openapi_admin",
            "openapi_admin@test.com",
            ct: ct
        );
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private void AuthenticateAdmin() =>
        IntegrationAuthHelper.Authenticate(
            _client,
            userId: _adminUser.Id,
            tenantId: _adminTenant.Id,
            username: _adminUser.Username.Value,
            role: "PlatformAdmin",
            email: _adminUser.Email.Value,
            subject: _adminUser.KeycloakUserId
        );

    [Fact]
    public async Task OpenApi_Endpoint_ReturnsJsonDocument()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/openapi/v1.json", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync(ct);
        content.ShouldContain("openapi");
        content.ShouldContain("paths");
        content.ShouldContain("ApiProblemDetails");
        content.ShouldContain("application/problem+json");
    }

    [Fact]
    public async Task OpenApi_ContainsGlobalErrorResponsesForRestEndpoints()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/openapi/v1.json", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(content);

        var paths = doc.RootElement.GetProperty("paths");
        var productReviewsPath = paths
            .EnumerateObject()
            .FirstOrDefault(p =>
                p.Name.Contains("product-reviews", StringComparison.OrdinalIgnoreCase)
            )
            .Value;

        productReviewsPath.ValueKind.ShouldBe(JsonValueKind.Object);

        var productReviewsPost = productReviewsPath.GetProperty("post");
        var responses = productReviewsPost.GetProperty("responses");

        responses.TryGetProperty(StatusCodes.Status400BadRequest.ToString(), out _).ShouldBeTrue();
        responses
            .TryGetProperty(StatusCodes.Status401Unauthorized.ToString(), out _)
            .ShouldBeTrue();
        responses.TryGetProperty(StatusCodes.Status403Forbidden.ToString(), out _).ShouldBeTrue();
        responses.TryGetProperty(StatusCodes.Status404NotFound.ToString(), out _).ShouldBeTrue();
        responses
            .TryGetProperty(StatusCodes.Status500InternalServerError.ToString(), out _)
            .ShouldBeTrue();
    }

    [Fact]
    public async Task OpenApi_ContainsOAuth2SecurityScheme()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/openapi/v1.json", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(content);

        var components = doc.RootElement.GetProperty("components");
        var securitySchemes = components.GetProperty("securitySchemes");
        securitySchemes.TryGetProperty("OAuth2-Scalar", out var oauth2Scalar).ShouldBeTrue();
        oauth2Scalar.GetProperty("type").GetString().ShouldBe("oauth2");

        securitySchemes.TryGetProperty("OAuth2-Public", out var oauth2Public).ShouldBeTrue();
        oauth2Public.GetProperty("type").GetString().ShouldBe("oauth2");
    }

    [Fact]
    public async Task Scalar_Endpoint_ReturnsHtml()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/scalar/v1", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync(ct);
        content.ShouldContain("scalar");
    }

    [Fact]
    public async Task GraphQL_Endpoint_IsAccessible()
    {
        var ct = TestContext.Current.CancellationToken;
        AuthenticateAdmin();

        var response = await _client.PostAsJsonAsync(
            "/graphql",
            new { query = "{ __typename }" },
            ct
        );

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RequestContext_WhenCorrelationHeaderProvided_EchoesHeader()
    {
        var ct = TestContext.Current.CancellationToken;
        using var request = new HttpRequestMessage(HttpMethod.Get, "/openapi/v1.json");
        request.Headers.Add(RequestContextConstants.Headers.CorrelationId, "corr-edge-123");

        var response = await _client.SendAsync(request, ct);

        response.IsSuccessStatusCode.ShouldBeTrue();
        response
            .Headers.GetValues(RequestContextConstants.Headers.CorrelationId)
            .Single()
            .ShouldBe("corr-edge-123");
        response
            .Headers.GetValues(RequestContextConstants.Headers.TraceId)
            .Single()
            .ShouldNotBeNullOrWhiteSpace();
        response
            .Headers.GetValues(RequestContextConstants.Headers.ElapsedMs)
            .Single()
            .ShouldNotBeNullOrWhiteSpace();
    }
}
