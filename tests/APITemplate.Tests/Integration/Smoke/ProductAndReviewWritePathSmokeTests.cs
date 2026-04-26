using System.Net;
using System.Net.Http.Json;
using APITemplate.Tests.Integration.Helpers;
using ProductCatalog.Features.Category.Shared;
using ProductCatalog.Features.Product.Shared;
using Reviews.Domain;
using SharedKernel.Application.DTOs;
using SharedKernel.Contracts.Security;
using SharedKernel.Domain.Common;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Smoke;

[Collection("Smoke")]
[Trait("Category", "Smoke")]
[Trait("Docker", "true")]
public sealed class ProductAndReviewWritePathSmokeTests : SmokeTestBase
{
    public ProductAndReviewWritePathSmokeTests(CustomWebApplicationFactory factory)
        : base(factory) { }

    protected override string UsernamePrefix => "smoke-write";

    [Fact]
    public async Task Categories_CreateReadDelete_Works()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AuthenticateAsSeededUser([
            Permission.Categories.Read,
            Permission.Categories.Create,
            Permission.Categories.Delete,
        ]);

        string name = $"SmokeCat-{Guid.NewGuid():N}";
        HttpResponseMessage create = await Client.PostAsJsonAsync(
            "/api/v1/categories",
            new { Items = new[] { new { Name = name, Description = "smoke" } } },
            ct
        );
        await create.ShouldBeStatusAsync(HttpStatusCode.OK, ct);
        BatchResponse createBatch = await create.ReadJsonAsync<BatchResponse>(ct);
        createBatch.Failures.ShouldBeEmpty();

        Guid id = await ResolveCategoryIdAsync(name, ct);
        HttpResponseMessage byId = await Client.GetAsync($"/api/v1/categories/{id}", ct);
        await byId.ShouldBeStatusAsync(HttpStatusCode.OK, ct);

        HttpRequestMessage deleteRequest = new(HttpMethod.Delete, "/api/v1/categories")
        {
            Content = JsonContent.Create(new { Ids = new[] { id } }),
        };
        HttpResponseMessage delete = await Client.SendAsync(deleteRequest, ct);
        await delete.ShouldBeStatusAsync(HttpStatusCode.OK, ct);
    }

    [Fact]
    public async Task Products_CreateReadDelete_Works()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AuthenticateAsSeededUser([
            Permission.Products.Read,
            Permission.Products.Create,
            Permission.Products.Delete,
        ]);

        string name = $"SmokeProduct-{Guid.NewGuid():N}";
        HttpResponseMessage create = await Client.PostAsJsonAsync(
            "/api/v1/products",
            new
            {
                Items = new[]
                {
                    new
                    {
                        Name = name,
                        Description = "smoke",
                        Price = 11m,
                    },
                },
            },
            ct
        );
        await create.ShouldBeStatusAsync(HttpStatusCode.OK, ct);

        Guid id = await ResolveProductIdAsync(name, ct);
        HttpResponseMessage byId = await Client.GetAsync($"/api/v1/products/{id}", ct);
        await byId.ShouldBeStatusAsync(HttpStatusCode.OK, ct);

        HttpRequestMessage deleteRequest = new(HttpMethod.Delete, "/api/v1/products")
        {
            Content = JsonContent.Create(new { Ids = new[] { id } }),
        };
        HttpResponseMessage delete = await Client.SendAsync(deleteRequest, ct);
        await delete.ShouldBeStatusAsync(HttpStatusCode.OK, ct);
    }

    [Fact]
    public async Task ProductReviews_CreateReadDelete_Works()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AuthenticateAsSeededUser([
            Permission.Products.Read,
            Permission.Products.Create,
            Permission.ProductReviews.Read,
            Permission.ProductReviews.Create,
            Permission.ProductReviews.Delete,
        ]);

        Guid productId = await CreateProductAsync($"SmokeReviewProduct-{Guid.NewGuid():N}", ct);

        HttpResponseMessage createReview = await Client.PostAsJsonAsync(
            "/api/v1/product-reviews",
            new
            {
                ProductId = productId,
                Comment = "smoke review",
                Rating = 5,
            },
            ct
        );
        await createReview.ShouldBeStatusAsync(HttpStatusCode.Created, ct);
        ProductReviewResponse review = await createReview.ReadJsonAsync<ProductReviewResponse>(ct);

        HttpResponseMessage byProduct = await Client.GetAsync(
            $"/api/v1/product-reviews/by-product/{productId}",
            ct
        );
        await byProduct.ShouldBeStatusAsync(HttpStatusCode.OK, ct);

        HttpResponseMessage deleteReview = await Client.DeleteAsync(
            $"/api/v1/product-reviews/{review.Id}",
            ct
        );
        await deleteReview.ShouldBeStatusAsync(HttpStatusCode.NoContent, ct);
    }

    private async Task<Guid> CreateProductAsync(string name, CancellationToken ct)
    {
        HttpResponseMessage create = await Client.PostAsJsonAsync(
            "/api/v1/products",
            new
            {
                Items = new[]
                {
                    new
                    {
                        Name = name,
                        Description = "smoke",
                        Price = 10m,
                    },
                },
            },
            ct
        );
        await create.ShouldBeStatusAsync(HttpStatusCode.OK, ct);
        return await ResolveProductIdAsync(name, ct);
    }

    private async Task<Guid> ResolveProductIdAsync(string name, CancellationToken ct)
    {
        HttpResponseMessage response = await Client.GetAsync(
            $"/api/v1/products?name={Uri.EscapeDataString(name)}",
            ct
        );
        await response.ShouldBeStatusAsync(HttpStatusCode.OK, ct);
        ProductsResponse payload = await response.ReadJsonAsync<ProductsResponse>(ct);
        ProductResponse? product = payload.Page.Items.FirstOrDefault(p => p.Name == name);
        product.ShouldNotBeNull();
        return product!.Id;
    }

    private async Task<Guid> ResolveCategoryIdAsync(string name, CancellationToken ct)
    {
        HttpResponseMessage response = await Client.GetAsync(
            $"/api/v1/categories?name={Uri.EscapeDataString(name)}",
            ct
        );
        await response.ShouldBeStatusAsync(HttpStatusCode.OK, ct);
        PagedResponse<CategoryResponse> payload = await response.ReadJsonAsync<
            PagedResponse<CategoryResponse>
        >(ct);
        CategoryResponse? category = payload.Items.FirstOrDefault(i => i.Name == name);
        category.ShouldNotBeNull();
        return category!.Id;
    }
}
