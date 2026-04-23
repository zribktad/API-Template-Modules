using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Identity.Auth.Security;
using Microsoft.AspNetCore.Http;
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

        var problem = await ReadProblemAsync(response, ct);
        string message = ExtractValidationMessage(problem);
        string errorCode = ExtractErrorCode(problem);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem.Title.ShouldNotBeNullOrWhiteSpace();
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

        var problem = await ReadProblemAsync(response, ct);
        string message = ExtractValidationMessage(problem);
        string errorCode = ExtractErrorCode(problem);

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

        var problem = await ReadProblemAsync(response, ct);
        string message = ExtractValidationMessage(problem);
        string errorCode = ExtractErrorCode(problem);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem.Title.ShouldNotBeNullOrWhiteSpace();
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

        var problem = await ReadProblemAsync(response, ct);
        string message = ExtractValidationMessage(problem);
        string errorCode = ExtractErrorCode(problem);

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

        var problem = await ReadProblemAsync(response, ct);
        string message = ExtractValidationMessage(problem);
        string errorCode = ExtractErrorCode(problem);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem.Title.ShouldNotBeNullOrWhiteSpace();
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

        var problem = await ReadProblemAsync(response, ct);
        string message = ExtractValidationMessage(problem);
        string errorCode = ExtractErrorCode(problem);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        message
            .Contains("MinRating must be between 1 and 5", StringComparison.OrdinalIgnoreCase)
            .ShouldBeTrue();
        errorCode.ShouldBe("GEN-0400");
    }

    [Theory]
    [InlineData("", "user@example.com", "Username")]
    [InlineData("valid-user", "not-an-email", "Email")]
    public async Task UsersController_CreateWithInvalidField_ReturnsUnifiedProblemDetails(
        string username,
        string email,
        string expectedFieldInMessage
    )
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(
            _client,
            username: $"{AuthConstants.Claims.ServiceAccountUsernamePrefix}boundary-validation",
            permissions: [Permission.Users.Create]
        );

        var response = await _client.PostAsJsonAsync(
            "/api/v1/users",
            new { Username = username, Email = email },
            ct
        );

        var problem = await ReadProblemAsync(response, ct);
        string message = ExtractValidationMessage(problem);
        string errorCode = ExtractErrorCode(problem);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem.Title.ShouldNotBeNullOrWhiteSpace();
        message
            .Contains(expectedFieldInMessage, StringComparison.OrdinalIgnoreCase)
            .ShouldBeTrue(message);
        errorCode.ShouldBe("GEN-0400");
    }

    [Fact]
    public async Task GraphQL_Products_WithInvalidMinPrice_ReturnsGEN0400ValidationError()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(
            _client,
            username: $"{AuthConstants.Claims.ServiceAccountUsernamePrefix}graphql-validation"
        );

        var requestBody = JsonSerializer.Serialize(new
        {
            query = @"query($input: ProductQueryInput) { products(input: $input) { page { items { id } } } }",
            variables = new { input = new { minPrice = -1.0, pageNumber = 1, pageSize = 5 } },
        });
        using StringContent content = new(requestBody, Encoding.UTF8, "application/json");

        HttpResponseMessage response = await _client.PostAsync("/graphql", content, ct);
        string responseBody = await response.Content.ReadAsStringAsync(ct);

        using JsonDocument doc = JsonDocument.Parse(responseBody);
        doc.RootElement.TryGetProperty("errors", out JsonElement errorsElement).ShouldBeTrue(
            $"Expected GraphQL errors but got: {responseBody}"
        );
        errorsElement.GetArrayLength().ShouldBeGreaterThan(0, responseBody);
        errorsElement[0]
            .TryGetProperty("extensions", out JsonElement ext)
            .ShouldBeTrue($"No extensions on error. Body: {responseBody}");
        ext.TryGetProperty("code", out JsonElement codeElement).ShouldBeTrue(
            $"No code in extensions. Body: {responseBody}"
        );
        codeElement.GetString().ShouldBe("GEN-0400", responseBody);
    }

    [Fact]
    public async Task GraphQL_Products_WithValidFilter_ReturnsData()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(
            _client,
            username: $"{AuthConstants.Claims.ServiceAccountUsernamePrefix}graphql-validation"
        );

        var requestBody = JsonSerializer.Serialize(new
        {
            query = @"query($input: ProductQueryInput) { products(input: $input) { page { items { id name } totalCount } } }",
            variables = new { input = new { pageNumber = 1, pageSize = 5 } },
        });
        using StringContent content = new(requestBody, Encoding.UTF8, "application/json");

        HttpResponseMessage response = await _client.PostAsync("/graphql", content, ct);
        string responseBody = await response.Content.ReadAsStringAsync(ct);

        using JsonDocument doc = JsonDocument.Parse(responseBody);
        doc.RootElement.TryGetProperty("errors", out _).ShouldBeFalse(
            $"Expected no GraphQL errors but got: {responseBody}"
        );
        doc.RootElement.TryGetProperty("data", out JsonElement dataElement).ShouldBeTrue(
            $"Expected data in response. Body: {responseBody}"
        );
        dataElement.GetProperty("products").ValueKind.ShouldNotBe(
            JsonValueKind.Null,
            responseBody
        );
    }

    private static async Task<HttpValidationProblemDetails> ReadProblemAsync(
        HttpResponseMessage response,
        CancellationToken ct
    )
    {
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(ct);
        problem.ShouldNotBeNull();
        return problem;
    }

    private static string ExtractErrorCode(HttpValidationProblemDetails problem)
    {
        return
            problem.Extensions.TryGetValue("errorCode", out object? code) && code is JsonElement je
            ? je.GetString() ?? string.Empty
            : code?.ToString() ?? string.Empty;
    }

    private static string ExtractValidationMessage(HttpValidationProblemDetails problem)
    {
        if (!string.IsNullOrWhiteSpace(problem.Detail))
            return problem.Detail;

        if (problem.Errors.Count > 0)
        {
            return string.Join(" ", problem.Errors.SelectMany(e => e.Value));
        }

        return string.Empty;
    }
}
