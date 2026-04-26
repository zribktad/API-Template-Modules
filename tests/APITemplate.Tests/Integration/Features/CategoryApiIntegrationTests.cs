using System.Net;
using System.Net.Http.Json;
using APITemplate.Tests.Integration.Helpers;
using Identity.Directory.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using ProductCatalog.Features.Category.GetCategoryStats;
using ProductCatalog.Features.Category.Shared;
using SharedKernel.Application.DTOs;
using SharedKernel.Contracts.Security;
using SharedKernel.Domain.Common;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Features;

[Trait("Category", "Integration")]
[Trait("Docker", "true")]
[Collection(IntegrationCollectionNames.HttpStateful)]
public sealed class CategoryApiIntegrationTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private Tenant _tenant = default!;
    private AppUser _user = default!;

    public CategoryApiIntegrationTests(CustomWebApplicationFactory factory)
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
            "category_admin",
            "category_admin@test.com",
            ct: ct
        );
        Authenticate(
            Permission.Categories.Read,
            Permission.Categories.Create,
            Permission.Categories.Update,
            Permission.Categories.Delete
        );
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task CategoryCrudAndStats_Flow_Works()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string name = $"Category-{Guid.NewGuid():N}";

        HttpResponseMessage createResponse = await _client.PostAsJsonAsync(
            "/api/v1/categories",
            new { Items = new[] { new { Name = name, Description = "test" } } },
            ct
        );
        await createResponse.ShouldBeStatusAsync(HttpStatusCode.OK, ct);

        BatchResponse createBatch = await createResponse.ReadJsonAsync<BatchResponse>(ct);
        createBatch.Failures.ShouldBeEmpty();
        createBatch.SuccessCount.ShouldBe(1);

        Guid categoryId = await ResolveCategoryIdAsync(name, ct);

        HttpResponseMessage byIdResponse = await _client.GetAsync(
            $"/api/v1/categories/{categoryId}",
            ct
        );
        await byIdResponse.ShouldBeStatusAsync(HttpStatusCode.OK, ct);
        CategoryResponse category = await byIdResponse.ReadJsonAsync<CategoryResponse>(ct);
        category.Id.ShouldBe(categoryId);
        category.Name.ShouldBe(name);

        HttpResponseMessage updateResponse = await _client.PutAsJsonAsync(
            "/api/v1/categories",
            new
            {
                Items = new[]
                {
                    new
                    {
                        Id = categoryId,
                        Name = "Updated Category",
                        Description = "updated",
                    },
                },
            },
            ct
        );
        await updateResponse.ShouldBeStatusAsync(HttpStatusCode.OK, ct);
        BatchResponse updateBatch = await updateResponse.ReadJsonAsync<BatchResponse>(ct);
        updateBatch.Failures.ShouldBeEmpty();

        HttpResponseMessage statsResponse = await _client.GetAsync(
            $"/api/v1/categories/{categoryId}/stats",
            ct
        );
        await statsResponse.ShouldBeStatusAsync(HttpStatusCode.OK, ct);
        ProductCategoryStatsResponse stats =
            await statsResponse.ReadJsonAsync<ProductCategoryStatsResponse>(ct);
        stats.CategoryId.ShouldBe(categoryId);

        HttpRequestMessage deleteRequest = new(HttpMethod.Delete, "/api/v1/categories")
        {
            Content = JsonContent.Create(new { Ids = new[] { categoryId } }),
        };
        HttpResponseMessage deleteResponse = await _client.SendAsync(deleteRequest, ct);
        await deleteResponse.ShouldBeStatusAsync(HttpStatusCode.OK, ct);

        HttpResponseMessage deletedResponse = await _client.GetAsync(
            $"/api/v1/categories/{categoryId}",
            ct
        );
        await deletedResponse.ShouldBeStatusAsync(HttpStatusCode.NotFound, ct);
    }

    [Fact]
    public async Task GetCategoryById_WhenMissing_ReturnsNotFound()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/categories/{Guid.NewGuid()}",
            ct
        );
        await response.ShouldBeStatusAsync(HttpStatusCode.NotFound, ct);
    }

    [Fact]
    public async Task CreateCategory_WithInvalidPayload_ReturnsValidationFailure()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/categories",
            new { Items = new[] { new { Name = "" } } },
            ct
        );

        response.StatusCode.ShouldBeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.UnprocessableEntity
        );
    }

    [Fact]
    public async Task CreateCategory_WithoutCreatePermission_ReturnsForbidden()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Authenticate(Permission.Categories.Read);

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/categories",
            new { Items = new[] { new { Name = "NoPermission", Description = "x" } } },
            ct
        );

        await response.ShouldBeStatusAsync(HttpStatusCode.Forbidden, ct);
    }

    private void Authenticate(params string[] permissions) =>
        _client.WithAuth(
            "PlatformAdmin",
            userId: _user.Id,
            tenantId: _tenant.Id,
            username: _user.Username.Value,
            permissions: permissions,
            email: _user.Email.Value,
            subject: _user.KeycloakUserId
        );

    private async Task<Guid> ResolveCategoryIdAsync(string name, CancellationToken ct)
    {
        HttpResponseMessage response = await _client.GetAsync(
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
