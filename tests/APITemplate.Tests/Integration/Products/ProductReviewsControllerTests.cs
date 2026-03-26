using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using APITemplate.Tests.Integration.Helpers;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Products;

public class ProductReviewsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly Guid _tenantId = Guid.NewGuid();

    public ProductReviewsControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task FullReviewFlow_CreateAndQuery()
    {
        var ct = TestContext.Current.CancellationToken;
        var userId = IntegrationAuthHelper.AuthenticateAndGetUserId(_client, tenantId: _tenantId);

        // 1. Create a product
        var productName = $"Reviewed Product-{Guid.NewGuid():N}";
        var productResponse = await _client.PostAsJsonAsync(
            "/api/v1/products",
            new { Items = new[] { new { Name = productName, Price = 49.99 } } },
            ct
        );

        var productBody = await productResponse.Content.ReadAsStringAsync(ct);
        productResponse.StatusCode.ShouldBe(HttpStatusCode.OK, productBody);
        var productId = await ResolveProductIdAsync(productName, 49.99m, ct);

        // 2. Create a review for the product
        var createReviewResponse = await _client.PostAsJsonAsync(
            "/api/v1/productreviews",
            new
            {
                ProductId = productId,
                Comment = "Great product!",
                Rating = 5,
            },
            ct
        );

        createReviewResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await createReviewResponse.Content.ReadFromJsonAsync<ProductReviewResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        created.ShouldNotBeNull();
        var reviewId = created!.Id;
        created.UserId.ShouldBe(userId);
        created.Rating.ShouldBe(5);
        created.ProductId.ShouldBe(productId);

        // 3. Get review by id
        var getByIdResponse = await _client.GetAsync($"/api/v1/productreviews/{reviewId}", ct);
        getByIdResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var fetched = await getByIdResponse.Content.ReadFromJsonAsync<ProductReviewResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        fetched.ShouldNotBeNull();
        fetched!.UserId.ShouldBe(userId);

        // 4. Get reviews by productId
        var byProductResponse = await _client.GetAsync(
            $"/api/v1/productreviews/by-product/{productId}",
            ct
        );
        byProductResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var reviews = await byProductResponse.Content.ReadFromJsonAsync<ProductReviewResponse[]>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        reviews.ShouldNotBeNull();
        reviews!.Length.ShouldBeGreaterThanOrEqualTo(1);

        // 5. Delete the review
        var deleteResponse = await _client.DeleteAsync($"/api/v1/productreviews/{reviewId}", ct);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // 6. Verify deletion
        var getDeletedResponse = await _client.GetAsync($"/api/v1/productreviews/{reviewId}", ct);
        getDeletedResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_NonExistentReview_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var response = await _client.GetAsync($"/api/v1/productreviews/{Guid.NewGuid()}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_WithNonExistentProduct_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/productreviews",
            new { ProductId = Guid.NewGuid(), Rating = 3 },
            ct
        );

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        problem.ShouldNotBeNull();
        problem!.Status.ShouldBe((int)HttpStatusCode.NotFound);
        problem.Title.ShouldBe("Not Found");
        problem.Detail.ShouldNotBeNullOrWhiteSpace();
        problem.Detail.ShouldContain("Product with id");
        problem.Detail.ShouldContain("not found");
        problem.ErrorCode.ShouldBe(ErrorCatalog.Reviews.ProductNotFoundForReview);
        problem.Type.ShouldBe(
            $"https://api-template.local/errors/{ErrorCatalog.Reviews.ProductNotFoundForReview}"
        );
        problem.TraceId.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetByProductId_ReturnsEmptyForProductWithNoReviews()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var productName = $"No Review Product-{Guid.NewGuid():N}";
        var productResponse = await _client.PostAsJsonAsync(
            "/api/v1/products",
            new { Items = new[] { new { Name = productName, Price = 9.99 } } },
            ct
        );

        var productBody = await productResponse.Content.ReadAsStringAsync(ct);
        productResponse.StatusCode.ShouldBe(HttpStatusCode.OK, productBody);
        var productId = await ResolveProductIdAsync(productName, 9.99m, ct);

        var response = await _client.GetAsync($"/api/v1/productreviews/by-product/{productId}", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var reviews = await response.Content.ReadFromJsonAsync<ProductReviewResponse[]>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        reviews.ShouldNotBeNull();
        reviews!.ShouldBeEmpty();
    }

    [Fact]
    public async Task Create_WithInvalidRating_ReturnsBadRequestWithValidationError()
    {
        // FluentValidationActionFilter validates CreateProductReviewRequest before the handler runs.
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/productreviews",
            new { ProductId = Guid.NewGuid(), Rating = 0 },
            ct
        );

        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain("Rating");
    }

    [Fact]
    public async Task Delete_ReviewByOtherUser_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var ownerUserId = IntegrationAuthHelper.AuthenticateAndGetUserId(
            _client,
            tenantId: _tenantId
        );

        var productName = $"Shared Product-{Guid.NewGuid():N}";
        await _client.PostAsJsonAsync(
            "/api/v1/products",
            new { Items = new[] { new { Name = productName, Price = 10m } } },
            ct
        );
        var productId = await ResolveProductIdAsync(productName, 10m, ct);

        var createReviewResponse = await _client.PostAsJsonAsync(
            "/api/v1/productreviews",
            new { ProductId = productId, Rating = 4 },
            ct
        );
        createReviewResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await createReviewResponse.Content.ReadFromJsonAsync<ProductReviewResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        var reviewId = created!.Id;

        // Switch to a different user in the same tenant
        IntegrationAuthHelper.AuthenticateAndGetUserId(_client, tenantId: _tenantId);
        var deleteResponse = await _client.DeleteAsync($"/api/v1/productreviews/{reviewId}", ct);

        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private async Task<Guid> ResolveProductIdAsync(string name, decimal price, CancellationToken ct)
    {
        var response = await _client.GetAsync(
            $"/api/v1/products?name={Uri.EscapeDataString(name)}",
            ct
        );
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ProductsResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        payload.ShouldNotBeNull();
        var item = payload!.Page.Items.FirstOrDefault(p => p.Name == name && p.Price == price);
        item.ShouldNotBeNull();
        return item!.Id;
    }
}
