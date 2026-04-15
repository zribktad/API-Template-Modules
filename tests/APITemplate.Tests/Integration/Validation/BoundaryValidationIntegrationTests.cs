using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using APITemplate.Tests.Integration.Helpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Validation;

[Trait("Category", "Integration.Docker")]
public sealed class BoundaryValidationIntegrationTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public BoundaryValidationIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
    }

    [Fact]
    public async Task RolesController_InvalidBody_ReturnsUnifiedProblemDetails()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(
            _client,
            tenantId: Guid.NewGuid(),
            role: "TenantAdmin",
            permissions: new[] { "Roles.Create" }
        );

        var response = await _client.PostAsJsonAsync(
            "/api/v1/roles",
            new
            {
                Name = "",
                Permissions = new[] { "Users.Read" },
            },
            ct
        );

        JsonElement problem = await ReadProblemAsync(response, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem.GetProperty("title").GetString().ShouldBe("Bad Request");
        problem.GetProperty("detail").GetString().ShouldContain("required");
        problem.GetProperty("errorCode").GetString().ShouldBe("GEN-0400");
    }

    [Fact]
    public async Task ProductsController_InvalidQueryFilter_ReturnsUnifiedProblemDetails()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: Guid.NewGuid());

        var response = await _client.GetAsync(
            "/api/v1/products?sortBy=not-a-field&sortDirection=asc",
            ct
        );

        JsonElement problem = await ReadProblemAsync(response, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem.GetProperty("title").GetString().ShouldBe("Bad Request");
        problem.GetProperty("detail").GetString().ShouldContain("SortBy must be one of");
        problem.GetProperty("errorCode").GetString().ShouldBe("GEN-0400");
    }

    [Fact]
    public async Task ProductReviewsController_InvalidBody_ReturnsUnifiedProblemDetails()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: Guid.NewGuid());

        var response = await _client.PostAsJsonAsync(
            "/api/v1/product-reviews",
            new
            {
                ProductId = Guid.NewGuid(),
                Rating = 0,
            },
            ct
        );

        JsonElement problem = await ReadProblemAsync(response, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem.GetProperty("title").GetString().ShouldBe("Bad Request");
        problem.GetProperty("detail").GetString().ShouldContain("Rating must be between 1 and 5.");
        problem.GetProperty("errorCode").GetString().ShouldBe("GEN-0400");
    }

    private static async Task<JsonElement> ReadProblemAsync(
        HttpResponseMessage response,
        CancellationToken ct
    )
    {
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return document.RootElement.Clone();
    }
}
