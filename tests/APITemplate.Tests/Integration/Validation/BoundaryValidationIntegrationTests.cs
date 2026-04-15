using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using APITemplate.Tests.Integration.Helpers;
using Identity.Auth.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using SharedKernel.Contracts.Security;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Validation;

[Trait("Category", "Integration.Docker")]
public sealed class BoundaryValidationIntegrationTests : IClassFixture<CustomWebApplicationFactory>
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
            username: $"{AuthConstants.Claims.ServiceAccountUsernamePrefix}boundary-validation",
            permissions: [Permission.Roles.Create]
        );

        var response = await _client.PostAsJsonAsync(
            "/api/v1/roles",
            new { Name = "", Permissions = new[] { "Users.Read" } },
            ct
        );

        JsonElement problem = await ReadProblemAsync(response, ct);
        string message = ExtractValidationMessage(problem);
        string title = ExtractTitle(problem) ?? string.Empty;
        string errorCode = problem.GetProperty("errorCode").GetString() ?? string.Empty;

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string.IsNullOrWhiteSpace(title).ShouldBeFalse();
        message.Contains("required", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        errorCode.ShouldBe("GEN-0400");
    }

    [Fact]
    public async Task RolesController_InvalidPermissionsItems_ReturnsUnifiedProblemDetails()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(
            _client,
            username: $"{AuthConstants.Claims.ServiceAccountUsernamePrefix}boundary-validation",
            permissions: [Permission.Roles.Create]
        );

        var response = await _client.PostAsJsonAsync(
            "/api/v1/roles",
            new { Name = "Tenant Admin", Permissions = new[] { "Users.Read", " " } },
            ct
        );

        JsonElement problem = await ReadProblemAsync(response, ct);
        string message = ExtractValidationMessage(problem);
        string errorCode = problem.GetProperty("errorCode").GetString() ?? string.Empty;

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        message
            .Contains("Permissions must not contain", StringComparison.OrdinalIgnoreCase)
            .ShouldBeTrue();
        errorCode.ShouldBe("GEN-0400");
    }

    [Fact]
    public async Task ProductsController_InvalidQueryFilter_ReturnsUnifiedProblemDetails()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(
            _client,
            username: $"{AuthConstants.Claims.ServiceAccountUsernamePrefix}boundary-validation",
            permissions: [Permission.Products.Read]
        );

        var response = await _client.GetAsync(
            "/api/v1/products?sortBy=not-a-field&sortDirection=asc",
            ct
        );

        JsonElement problem = await ReadProblemAsync(response, ct);
        string message = ExtractValidationMessage(problem);
        string title = ExtractTitle(problem) ?? string.Empty;
        string errorCode = problem.GetProperty("errorCode").GetString() ?? string.Empty;

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string.IsNullOrWhiteSpace(title).ShouldBeFalse();
        message
            .Contains("SortBy must be one of", StringComparison.OrdinalIgnoreCase)
            .ShouldBeTrue();
        errorCode.ShouldBe("GEN-0400");
    }

    [Fact]
    public async Task ProductsController_InvalidCategoryIdsFilter_ReturnsUnifiedProblemDetails()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(
            _client,
            username: $"{AuthConstants.Claims.ServiceAccountUsernamePrefix}boundary-validation",
            permissions: [Permission.Products.Read]
        );

        var response = await _client.GetAsync($"/api/v1/products?categoryIds={Guid.Empty}", ct);

        JsonElement problem = await ReadProblemAsync(response, ct);
        string message = ExtractValidationMessage(problem);
        string errorCode = problem.GetProperty("errorCode").GetString() ?? string.Empty;

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        message
            .Contains(
                "CategoryIds cannot contain an empty value.",
                StringComparison.OrdinalIgnoreCase
            )
            .ShouldBeTrue();
        errorCode.ShouldBe("GEN-0400");
    }

    [Fact]
    public async Task ProductReviewsController_InvalidBody_ReturnsUnifiedProblemDetails()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(
            _client,
            username: $"{AuthConstants.Claims.ServiceAccountUsernamePrefix}boundary-validation",
            permissions: [Permission.ProductReviews.Create]
        );

        var response = await _client.PostAsJsonAsync(
            "/api/v1/product-reviews",
            new { ProductId = Guid.NewGuid(), Rating = 0 },
            ct
        );

        JsonElement problem = await ReadProblemAsync(response, ct);
        string message = ExtractValidationMessage(problem);
        string title = ExtractTitle(problem) ?? string.Empty;
        string errorCode = problem.GetProperty("errorCode").GetString() ?? string.Empty;

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string.IsNullOrWhiteSpace(title).ShouldBeFalse();
        message
            .Contains("Rating must be between 1 and 5.", StringComparison.OrdinalIgnoreCase)
            .ShouldBeTrue();
        errorCode.ShouldBe("GEN-0400");
    }

    [Fact]
    public async Task ProductReviewsController_InvalidQueryRange_ReturnsUnifiedProblemDetails()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(
            _client,
            username: $"{AuthConstants.Claims.ServiceAccountUsernamePrefix}boundary-validation",
            permissions: [Permission.ProductReviews.Read]
        );

        var response = await _client.GetAsync("/api/v1/product-reviews?minRating=0", ct);

        JsonElement problem = await ReadProblemAsync(response, ct);
        string message = ExtractValidationMessage(problem);
        string errorCode = problem.GetProperty("errorCode").GetString() ?? string.Empty;

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        message
            .Contains("MinRating must be between 1 and 5", StringComparison.OrdinalIgnoreCase)
            .ShouldBeTrue();
        errorCode.ShouldBe("GEN-0400");
    }

    private static async Task<JsonElement> ReadProblemAsync(
        HttpResponseMessage response,
        CancellationToken ct
    )
    {
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(ct)
        );
        return document.RootElement.Clone();
    }

    private static string? ExtractTitle(JsonElement problem)
    {
        return
            problem.TryGetProperty("title", out JsonElement title)
            && title.ValueKind == JsonValueKind.String
            ? title.GetString()
            : null;
    }

    private static string ExtractValidationMessage(JsonElement problem)
    {
        if (
            problem.TryGetProperty("detail", out JsonElement detail)
            && detail.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(detail.GetString())
        )
            return detail.GetString()!;

        if (
            problem.TryGetProperty("errors", out JsonElement errors)
            && errors.ValueKind == JsonValueKind.Object
        )
        {
            List<string> messages = [];
            foreach (JsonProperty fieldErrors in errors.EnumerateObject())
            {
                if (fieldErrors.Value.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (JsonElement error in fieldErrors.Value.EnumerateArray())
                {
                    if (error.ValueKind != JsonValueKind.String)
                        continue;

                    string? value = error.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                        messages.Add(value);
                }
            }

            if (messages.Count > 0)
                return string.Join(" ", messages);
        }

        return string.Empty;
    }
}
