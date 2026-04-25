using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using APITemplate.Tests.Integration.Helpers;
using Identity.Directory.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using ProductCatalog.Features.Product.Shared;
using SharedKernel.Application.DTOs;
using SharedKernel.Contracts.Security;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Auth;

[Trait("Category", "Integration")]
[Trait("Docker", "true")]
[Collection(IntegrationCollectionNames.HttpStateful)]
public class AuthenticatedCrudTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private Tenant _adminTenant = default!;
    private AppUser _adminUser = default!;

    public AuthenticatedCrudTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
    }

    public async ValueTask InitializeAsync()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (_adminTenant, _adminUser) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            "crud_admin",
            "crud_admin@test.com",
            ct: ct
        );
        AuthenticateAdmin();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private void AuthenticateAdmin() =>
        IntegrationAuthHelper.Authenticate(
            _client,
            userId: _adminUser.Id,
            tenantId: _adminTenant.Id,
            username: _adminUser.Username.Value,
            role: "PlatformAdmin",
            permissions:
            [
                Permission.Products.Read,
                Permission.Products.Create,
                Permission.Products.Update,
                Permission.Products.Delete,
            ],
            email: _adminUser.Email.Value,
            subject: _adminUser.KeycloakUserId
        );

    [Fact]
    public async Task FullCrudFlow_WorksWithAuthentication()
    {
        var ct = TestContext.Current.CancellationToken;

        // 2. Get all - empty
        var getAllResponse = await _client.GetAsync("/api/v1/products", ct);
        getAllResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var pagedEmpty = await getAllResponse.Content.ReadFromJsonAsync<ProductsResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        pagedEmpty.ShouldNotBeNull();
        pagedEmpty!.Page.Items.ShouldBeEmpty();

        // 3. Create product
        var productName = $"Test Product-{Guid.NewGuid():N}";
        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/products",
            new
            {
                Items = new[]
                {
                    new
                    {
                        Name = productName,
                        Description = "A description",
                        Price = 29.99,
                    },
                },
            },
            ct
        );

        var createBody = await createResponse.Content.ReadAsStringAsync(ct);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.OK, createBody);

        var createBatch = JsonSerializer.Deserialize<BatchResponse>(
            createBody,
            TestJsonOptions.CaseInsensitive
        );
        createBatch.ShouldNotBeNull();
        createBatch!.Failures.ShouldBeEmpty();
        var createdId = await ResolveProductIdAsync(productName, 29.99m, ct);

        // 4. Get by id
        var getByIdResponse = await _client.GetAsync($"/api/v1/products/{createdId}", ct);
        getByIdResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var fetched = await getByIdResponse.Content.ReadFromJsonAsync<ProductResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        fetched.ShouldNotBeNull();
        fetched!.Name.ShouldBe(productName);
        fetched.Price.ShouldBe(29.99m);

        // 5. Update product
        var updateResponse = await _client.PutAsJsonAsync(
            "/api/v1/products",
            new
            {
                Items = new[]
                {
                    new
                    {
                        Id = createdId,
                        Name = "Updated Product",
                        Description = "Updated desc",
                        Price = 39.99,
                    },
                },
            },
            ct
        );

        var updateBody = await updateResponse.Content.ReadAsStringAsync(ct);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK, updateBody);

        // 6. Verify update
        var verifyResponse = await _client.GetAsync($"/api/v1/products/{createdId}", ct);
        var updated = await verifyResponse.Content.ReadFromJsonAsync<ProductResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        updated.ShouldNotBeNull();
        updated!.Name.ShouldBe("Updated Product");
        updated.Price.ShouldBe(39.99m);

        // 7. Delete product
        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/products")
        {
            Content = JsonContent.Create(new { Ids = new[] { createdId } }),
        };
        var deleteResponse = await _client.SendAsync(deleteRequest, ct);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // 8. Verify deletion
        var getDeletedResponse = await _client.GetAsync($"/api/v1/products/{createdId}", ct);
        getDeletedResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_NonExistentProduct_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync($"/api/v1/products/{Guid.NewGuid()}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_MultipleProducts_AllReturnedInGetAll()
    {
        var ct = TestContext.Current.CancellationToken;

        await _client.PostAsJsonAsync(
            "/api/v1/products",
            new { Items = new[] { new { Name = "Product A", Price = 10.0 } } },
            ct
        );
        await _client.PostAsJsonAsync(
            "/api/v1/products",
            new { Items = new[] { new { Name = "Product B", Price = 20.0 } } },
            ct
        );

        var response = await _client.GetAsync("/api/v1/products", ct);
        var pagedResponse = await response.Content.ReadFromJsonAsync<ProductsResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        pagedResponse.ShouldNotBeNull();
        pagedResponse!.Page.Items.Count().ShouldBeGreaterThanOrEqualTo(2);
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
