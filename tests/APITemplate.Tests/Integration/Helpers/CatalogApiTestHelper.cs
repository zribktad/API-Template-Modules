using System.Net;
using System.Net.Http.Json;
using Identity.Directory.Entities;
using ProductCatalog.Features.Category.Shared;
using ProductCatalog.Features.Product.Shared;
using Reviews.Domain;
using SharedKernel.Application.DTOs;
using SharedKernel.Domain.Common;
using Shouldly;

namespace APITemplate.Tests.Integration.Helpers;

internal static class CatalogApiTestHelper
{
    internal static HttpClient AuthenticateAs(
        this HttpClient client,
        Tenant tenant,
        AppUser user,
        params string[] permissions
    )
    {
        return client.WithAuth(
            "PlatformAdmin",
            userId: user.Id,
            tenantId: tenant.Id,
            username: user.Username.Value,
            permissions: permissions,
            email: user.Email.Value,
            subject: user.KeycloakUserId
        );
    }

    internal static async Task<Guid> CreateCategoryAsync(
        this HttpClient client,
        string name,
        CancellationToken ct
    )
    {
        HttpResponseMessage create = await client.PostAsJsonAsync(
            "/api/v1/categories",
            new { Items = new[] { new { Name = name, Description = "test" } } },
            ct
        );
        await create.ShouldBeStatusAsync(HttpStatusCode.OK, ct);
        return await client.ResolveCategoryIdAsync(name, ct);
    }

    internal static async Task<Guid> ResolveCategoryIdAsync(
        this HttpClient client,
        string name,
        CancellationToken ct
    )
    {
        HttpResponseMessage response = await client.GetAsync(
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

    internal static async Task<Guid> CreateProductAsync(
        this HttpClient client,
        string name,
        CancellationToken ct
    )
    {
        HttpResponseMessage create = await client.PostAsJsonAsync(
            "/api/v1/products",
            new
            {
                Items = new[]
                {
                    new
                    {
                        Name = name,
                        Description = "test",
                        Price = 10m,
                    },
                },
            },
            ct
        );
        await create.ShouldBeStatusAsync(HttpStatusCode.OK, ct);
        return await client.ResolveProductIdAsync(name, ct);
    }

    internal static async Task<Guid> ResolveProductIdAsync(
        this HttpClient client,
        string name,
        CancellationToken ct
    )
    {
        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/products?name={Uri.EscapeDataString(name)}",
            ct
        );
        await response.ShouldBeStatusAsync(HttpStatusCode.OK, ct);

        ProductsResponse payload = await response.ReadJsonAsync<ProductsResponse>(ct);
        ProductResponse? product = payload.Page.Items.FirstOrDefault(p => p.Name == name);
        product.ShouldNotBeNull();
        return product!.Id;
    }

    internal static async Task<Guid> CreateImageProductDataAsync(
        this HttpClient client,
        CancellationToken ct
    )
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/product-data/image",
            new
            {
                Title = $"TestImage-{Guid.NewGuid():N}",
                Description = "test image",
                Width = 100,
                Height = 100,
                Format = "png",
                FileSizeBytes = 200L,
            },
            ct
        );
        await response.ShouldBeStatusAsync(HttpStatusCode.Created, ct);
        ProductDataContractResponse created =
            await response.ReadJsonAsync<ProductDataContractResponse>(ct);
        return created.Id;
    }
}
