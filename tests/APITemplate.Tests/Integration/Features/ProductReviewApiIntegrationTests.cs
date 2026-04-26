using System.Net;
using System.Net.Http.Json;
using APITemplate.Tests.Integration.Helpers;
using Identity.Directory.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using ProductCatalog.Features.Product.Shared;
using Reviews.Domain;
using SharedKernel.Application.DTOs;
using SharedKernel.Contracts.Security;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Features;

[Trait("Category", "Integration")]
[Trait("Docker", "true")]
[Collection(IntegrationCollectionNames.HttpStateful)]
public sealed class ProductReviewApiIntegrationTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private Tenant _tenant = default!;
    private AppUser _user = default!;

    public ProductReviewApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
    }

    public async ValueTask InitializeAsync()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (_tenant, _user) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            "review_admin",
            "review_admin@test.com",
            ct: ct
        );
        _client.AuthenticateAs(
            _tenant,
            _user,
            Permission.Products.Read,
            Permission.Products.Create,
            Permission.ProductReviews.Read,
            Permission.ProductReviews.Create,
            Permission.ProductReviews.Delete
        );
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task CreateReadDeleteReview_Flow_Works()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid productId = await _client.CreateProductAsync($"Review Product-{Guid.NewGuid():N}", ct);

        HttpResponseMessage createResponse = await _client.PostAsJsonAsync(
            "/api/v1/product-reviews",
            new
            {
                ProductId = productId,
                Comment = "solid",
                Rating = 5,
            },
            ct
        );
        await createResponse.ShouldBeStatusAsync(HttpStatusCode.Created, ct);
        ProductReviewResponse created = await createResponse.ReadJsonAsync<ProductReviewResponse>(
            ct
        );

        HttpResponseMessage byIdResponse = await _client.GetAsync(
            $"/api/v1/product-reviews/{created.Id}",
            ct
        );
        await byIdResponse.ShouldBeStatusAsync(HttpStatusCode.OK, ct);

        HttpResponseMessage byProductResponse = await _client.GetAsync(
            $"/api/v1/product-reviews/by-product/{productId}",
            ct
        );
        await byProductResponse.ShouldBeStatusAsync(HttpStatusCode.OK, ct);
        IReadOnlyList<ProductReviewResponse> byProduct = await byProductResponse.ReadJsonAsync<
            List<ProductReviewResponse>
        >(ct);
        byProduct.ShouldContain(r => r.Id == created.Id);

        HttpResponseMessage deleteResponse = await _client.DeleteAsync(
            $"/api/v1/product-reviews/{created.Id}",
            ct
        );
        await deleteResponse.ShouldBeStatusAsync(HttpStatusCode.NoContent, ct);

        HttpResponseMessage afterDelete = await _client.GetAsync(
            $"/api/v1/product-reviews/{created.Id}",
            ct
        );
        await afterDelete.ShouldBeStatusAsync(HttpStatusCode.NotFound, ct);
    }

    [Fact]
    public async Task CreateReview_ForMissingProduct_ReturnsNotFound()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/product-reviews",
            new
            {
                ProductId = Guid.NewGuid(),
                Comment = "missing",
                Rating = 4,
            },
            ct
        );
        await response.ShouldBeStatusAsync(HttpStatusCode.NotFound, ct);
    }

    [Fact]
    public async Task CreateReview_WithoutPermission_ReturnsForbidden()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid productId = await _client.CreateProductAsync($"NoPerm Product-{Guid.NewGuid():N}", ct);
        _client.AuthenticateAs(_tenant, _user, Permission.ProductReviews.Read);

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/product-reviews",
            new
            {
                ProductId = productId,
                Comment = "forbidden",
                Rating = 5,
            },
            ct
        );

        await response.ShouldBeStatusAsync(HttpStatusCode.Forbidden, ct);
    }

    [Fact]
    public async Task CreateReview_WithInvalidPayload_ReturnsValidationFailure()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid productId = await _client.CreateProductAsync(
            $"Invalid Review Product-{Guid.NewGuid():N}",
            ct
        );

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/product-reviews",
            new
            {
                ProductId = productId,
                Comment = "bad",
                Rating = 0,
            },
            ct
        );

        response.StatusCode.ShouldBeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.UnprocessableEntity
        );
    }
}
