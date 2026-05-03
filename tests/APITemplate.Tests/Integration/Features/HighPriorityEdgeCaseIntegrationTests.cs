using System.Net;
using System.Net.Http.Json;
using APITemplate.Tests.Integration.Helpers;
using BuildingBlocks.Application.DTOs;
using BuildingBlocks.Domain.Common;
using BuildingBlocks.Security;
using Identity.Directory.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using ProductCatalog.Features.Category.Shared;
using ProductCatalog.Features.Product.Shared;
using SharedKernel.Contracts.Queries.Reviews;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Features;

[Trait("Category", "Integration")]
[Trait("Docker", "true")]
[Collection(IntegrationCollectionNames.HttpStateful)]
public sealed class HighPriorityEdgeCaseIntegrationTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _tenantAClient;
    private readonly HttpClient _tenantBClient;

    private Tenant _tenantA = default!;
    private Tenant _tenantB = default!;
    private AppUser _userA = default!;
    private AppUser _userB = default!;

    public HighPriorityEdgeCaseIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _tenantAClient = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        _tenantBClient = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
    }

    public async ValueTask InitializeAsync()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (_tenantA, _userA) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            "tenant_a",
            "tenant_a@test.com",
            ct: ct
        );
        (_tenantB, _userB) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            "tenant_b",
            "tenant_b@test.com",
            ct: ct
        );

        string[] perms =
        [
            Permission.Products.Read,
            Permission.Products.Create,
            Permission.Products.Delete,
            Permission.Categories.Read,
            Permission.Categories.Create,
            Permission.Categories.Delete,
            Permission.ProductReviews.Read,
            Permission.ProductReviews.Create,
            Permission.ProductReviews.Delete,
            Permission.ProductData.Read,
            Permission.ProductData.Create,
            Permission.ProductData.Delete,
        ];

        _tenantAClient.AuthenticateAs(_tenantA, _userA, perms);
        _tenantBClient.AuthenticateAs(_tenantB, _userB, perms);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task TenantBoundary_ResourcesFromAnotherTenant_AreNotAccessible()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid categoryId = await _tenantAClient.CreateCategoryAsync(
            $"TenantA-Cat-{Guid.NewGuid():N}",
            ct
        );
        Guid productId = await _tenantAClient.CreateProductAsync(
            $"TenantA-Product-{Guid.NewGuid():N}",
            ct
        );
        Guid reviewId = await CreateReviewAsync(_tenantAClient, productId, ct);
        Guid productDataId = await _tenantAClient.CreateImageProductDataAsync(ct);

        HttpResponseMessage[] tenantBResponses = await Task.WhenAll(
            _tenantBClient.GetAsync($"/api/v1/categories/{categoryId}", ct),
            _tenantBClient.GetAsync($"/api/v1/products/{productId}", ct),
            _tenantBClient.GetAsync($"/api/v1/product-reviews/{reviewId}", ct),
            _tenantBClient.GetAsync($"/api/v1/product-data/{productDataId}", ct)
        );

        foreach (HttpResponseMessage response in tenantBResponses)
            await response.ShouldBeStatusAsync(HttpStatusCode.NotFound, ct);
    }

    [Fact]
    public async Task ProductData_SecondDeleteIsHandled()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid productDataId = await _tenantAClient.CreateImageProductDataAsync(ct);

        HttpResponseMessage firstProductDataDelete = await _tenantAClient.DeleteAsync(
            $"/api/v1/product-data/{productDataId}",
            ct
        );
        await firstProductDataDelete.ShouldBeStatusAsync(HttpStatusCode.NoContent, ct);

        HttpResponseMessage secondProductDataDelete = await _tenantAClient.DeleteAsync(
            $"/api/v1/product-data/{productDataId}",
            ct
        );
        await secondProductDataDelete.ShouldBeStatusAsync(HttpStatusCode.NotFound, ct);
    }

    private static async Task<Guid> CreateReviewAsync(
        HttpClient client,
        Guid productId,
        CancellationToken ct
    )
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/product-reviews",
            new
            {
                ProductId = productId,
                Comment = "tenant edge review",
                Rating = 5,
            },
            ct
        );
        await response.ShouldBeStatusAsync(HttpStatusCode.Created, ct);
        ProductReviewResponse review = await response.ReadJsonAsync<ProductReviewResponse>(ct);
        return review.Id;
    }
}
