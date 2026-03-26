using System.Net;
using System.Net.Http.Json;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using APITemplate.Tests.Integration.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Products;

public class ProductsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly Mock<IProductDataRepository> _productDataRepositoryMock;

    public ProductsControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _productDataRepositoryMock = factory.Services.GetRequiredService<
            Mock<IProductDataRepository>
        >();
        _productDataRepositoryMock.Reset();
    }

    [Fact]
    public async Task GetAll_WithValidToken_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var response = await _client.GetAsync("/api/v1/products", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ProductsResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        payload.ShouldNotBeNull();
        payload!.Page.Items.ShouldNotBeNull();
        payload.Facets.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateAndGetById_WithProductDataIds_RoundTripsIds()
    {
        var ct = TestContext.Current.CancellationToken;
        var productDataId = Guid.NewGuid();
        IntegrationAuthHelper.Authenticate(_client);

        _productDataRepositoryMock
            .Setup(r =>
                r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([new ImageProductData { Id = productDataId, Title = "Image" }]);

        var productName = $"Product with data-{Guid.NewGuid():N}";
        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/products",
            new
            {
                Items = new[]
                {
                    new
                    {
                        name = productName,
                        description = "Test product",
                        price = 25,
                        productDataIds = new[] { productDataId, productDataId },
                    },
                },
            },
            ct
        );

        var createBody = await createResponse.Content.ReadAsStringAsync(ct);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.OK, createBody);
        var createdId = await ResolveProductIdAsync(productName, 25m, null, ct);

        var getResponse = await _client.GetAsync($"/api/v1/products/{createdId}", ct);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<ProductResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        fetched.ShouldNotBeNull();
        fetched!.ProductDataIds.ShouldBe([productDataId]);
        fetched.Name.ShouldBe(productName);
    }

    [Fact]
    public async Task Create_WithInvalidProductDataId_ReturnsBadRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/products",
            new
            {
                Items = new[]
                {
                    new
                    {
                        name = "Invalid product data",
                        price = 25,
                        productDataIds = new[] { "bad-id" },
                    },
                },
            },
            ct
        );

        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
    }

    [Fact]
    public async Task Update_WithoutProductDataIds_PreservesExistingLinks()
    {
        var ct = TestContext.Current.CancellationToken;
        var productDataId = Guid.NewGuid();
        IntegrationAuthHelper.Authenticate(_client);

        _productDataRepositoryMock
            .Setup(r =>
                r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([new ImageProductData { Id = productDataId, Title = "Image" }]);

        var productName = $"Product with data-{Guid.NewGuid():N}";
        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/products",
            new
            {
                Items = new[]
                {
                    new
                    {
                        name = productName,
                        description = "Test product",
                        price = 25,
                        productDataIds = new[] { productDataId },
                    },
                },
            },
            ct
        );

        var createBody = await createResponse.Content.ReadAsStringAsync(ct);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.OK, createBody);
        var createdId = await ResolveProductIdAsync(productName, 25m, null, ct);

        var updateResponse = await _client.PutAsJsonAsync(
            "/api/v1/products",
            new
            {
                Items = new[]
                {
                    new
                    {
                        Id = createdId,
                        name = "Renamed product",
                        description = "Updated",
                        price = 30,
                    },
                },
            },
            ct
        );

        var updateBody = await updateResponse.Content.ReadAsStringAsync(ct);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK, updateBody);

        var getResponse = await _client.GetAsync($"/api/v1/products/{createdId}", ct);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<ProductResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        fetched.ShouldNotBeNull();
        fetched!.ProductDataIds.ShouldBe([productDataId]);
    }

    [Fact]
    public async Task GetAll_WithCategoryFilterAndFacets_ReturnsFilteredProductsAndFacetCounts()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var electronicsName = $"Electronics-{Guid.NewGuid():N}";
        var booksName = $"Books-{Guid.NewGuid():N}";

        var electronicsResponse = await _client.PostAsJsonAsync(
            "/api/v1/categories",
            new
            {
                Items = new[]
                {
                    new { Name = electronicsName, Description = "Devices and accessories" },
                },
            },
            ct
        );
        var booksResponse = await _client.PostAsJsonAsync(
            "/api/v1/categories",
            new { Items = new[] { new { Name = booksName, Description = "Printed books" } } },
            ct
        );

        electronicsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        booksResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var electronicsId = await ResolveCategoryIdAsync(electronicsName, ct);
        var booksId = await ResolveCategoryIdAsync(booksName, ct);

        await _client.PostAsJsonAsync(
            "/api/v1/products",
            new
            {
                Items = new[]
                {
                    new
                    {
                        Name = "Wireless Mouse",
                        Description = "Comfortable office mouse",
                        Price = 30,
                        CategoryId = electronicsId,
                    },
                },
            },
            ct
        );
        await _client.PostAsJsonAsync(
            "/api/v1/products",
            new
            {
                Items = new[]
                {
                    new
                    {
                        Name = "Wireless Keyboard",
                        Description = "Mechanical office keyboard",
                        Price = 80,
                        CategoryId = electronicsId,
                    },
                },
            },
            ct
        );
        await _client.PostAsJsonAsync(
            "/api/v1/products",
            new
            {
                Items = new[]
                {
                    new
                    {
                        Name = "Fantasy Novel",
                        Description = "Epic dragon story",
                        Price = 15,
                        CategoryId = booksId,
                    },
                },
            },
            ct
        );

        var response = await _client.GetAsync($"/api/v1/products?categoryIds={electronicsId}", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ProductsResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        payload.ShouldNotBeNull();

        payload!.Page.Items.Count().ShouldBe(2);
        payload
            .Page.Items.Select(item => item.Name)
            .ShouldBe(["Wireless Mouse", "Wireless Keyboard"], ignoreOrder: true);
        payload.Facets.Categories.Count.ShouldBeGreaterThanOrEqualTo(2);
        var electronicsFacet = payload.Facets.Categories.Single(c => c.CategoryId == electronicsId);
        electronicsFacet.Count.ShouldBe(2);
        payload.Facets.PriceBuckets.Single(bucket => bucket.Label == "0 - 50").Count.ShouldBe(1);
        payload.Facets.PriceBuckets.Single(bucket => bucket.Label == "50 - 100").Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetAll_AfterCreate_ReturnsNewProduct_WhenCacheIsInvalidated()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var initialResponse = await _client.GetAsync("/api/v1/products", ct);
        initialResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var initialPayload = await initialResponse.Content.ReadFromJsonAsync<ProductsResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        initialPayload.ShouldNotBeNull();

        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/products",
            new
            {
                Items = new[]
                {
                    new
                    {
                        name = "Cached product",
                        description = "Created while cache is warm",
                        price = 10,
                    },
                },
            },
            ct
        );

        var createBody = await createResponse.Content.ReadAsStringAsync(ct);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.OK, createBody);

        var secondResponse = await _client.GetAsync("/api/v1/products", ct);
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var secondPayload = await secondResponse.Content.ReadFromJsonAsync<ProductsResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        secondPayload.ShouldNotBeNull();
        secondPayload!.Page.Items.ShouldContain(p => p.Name == "Cached product");
    }

    [Fact]
    public async Task Create_WithEmptyProductName_ReturnsBadRequestWithValidationError()
    {
        // FluentValidationActionFilter validates CreateProductsRequest before the handler runs,
        // so empty Name is rejected at the controller level with 400.
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/products",
            new { Items = new[] { new { Name = "", Price = 10m } } },
            ct
        );

        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain("required");
    }

    [Fact]
    public async Task Create_WithPriceAboveThresholdAndNoDescription_ReturnsUnprocessableWithValidationFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/products",
            new { Items = new[] { new { Name = "Expensive Widget", Price = 1500m } } },
            ct
        );

        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity, body);

        var batch = await response.Content.ReadFromJsonAsync<BatchResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        batch.ShouldNotBeNull();
        batch!.FailureCount.ShouldBe(1);
        batch.Failures[0].Errors.ShouldContain(e => e.Contains("Description"));
    }

    [Fact]
    public async Task GetById_NonExistentProduct_ReturnsProblemDetailsBody()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var response = await _client.GetAsync($"/api/v1/products/{Guid.NewGuid()}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        problem.ShouldNotBeNull();
        problem!.Status.ShouldBe((int)HttpStatusCode.NotFound);
        problem.Title.ShouldBe("Not Found");
        problem.ErrorCode.ShouldNotBeNullOrWhiteSpace();
        problem.TraceId.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Delete_NonExistentProduct_ReturnsUnprocessableWithFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var missingId = Guid.NewGuid();
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/products")
        {
            Content = JsonContent.Create(new { Ids = new[] { missingId } }),
        };
        var response = await _client.SendAsync(request, ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity, body);

        var batch = await response.Content.ReadFromJsonAsync<BatchResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        batch.ShouldNotBeNull();
        batch!.FailureCount.ShouldBe(1);
        batch.Failures[0].Id.ShouldBe(missingId);
    }

    private async Task<Guid> ResolveProductIdAsync(
        string name,
        decimal price,
        Guid? categoryId,
        CancellationToken ct
    )
    {
        var url = $"/api/v1/products?name={Uri.EscapeDataString(name)}";
        if (categoryId.HasValue)
            url += $"&categoryIds={categoryId.Value}";

        var response = await _client.GetAsync(url, ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ProductsResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        payload.ShouldNotBeNull();
        var item = payload!.Page.Items.FirstOrDefault(p =>
            p.Name == name && p.Price == price && p.CategoryId == categoryId
        );
        item.ShouldNotBeNull();
        return item!.Id;
    }

    private async Task<Guid> ResolveCategoryIdAsync(string name, CancellationToken ct)
    {
        var response = await _client.GetAsync(
            $"/api/v1/categories?name={Uri.EscapeDataString(name)}",
            ct
        );
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<PagedResponse<CategoryResponse>>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        payload.ShouldNotBeNull();
        var item = payload!.Items.FirstOrDefault(c => c.Name == name);
        item.ShouldNotBeNull();
        return item!.Id;
    }
}
