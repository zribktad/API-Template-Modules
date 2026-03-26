using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using APITemplate.Tests.Integration.Helpers;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Security;

public class PermissionAuthorizationIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PermissionAuthorizationIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task User_CanGetProducts()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        IntegrationAuthHelper.AuthenticateAsUser(client);

        var response = await client.GetAsync("/api/v1/products", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task User_CannotCreateProduct()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        IntegrationAuthHelper.AuthenticateAsUser(client);

        var response = await client.PostAsJsonAsync(
            "/api/v1/products",
            new
            {
                Items = new[]
                {
                    new
                    {
                        Name = "Forbidden",
                        Description = "Should fail",
                        Price = 1.00,
                    },
                },
            },
            ct
        );

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TenantAdmin_CanCreateProduct()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        IntegrationAuthHelper.AuthenticateAsTenantAdmin(client);

        var response = await client.PostAsJsonAsync(
            "/api/v1/products",
            new
            {
                Items = new[]
                {
                    new
                    {
                        Name = "TenantAdmin Product",
                        Description = "Should succeed",
                        Price = 10.00,
                    },
                },
            },
            ct
        );

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TenantAdmin_CannotCreateUser()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        IntegrationAuthHelper.AuthenticateAsTenantAdmin(client);

        var response = await client.PostAsJsonAsync(
            "/api/v1/users",
            new
            {
                Username = "newuser",
                Email = "new@example.com",
                Password = "P@ssw0rd123!",
            },
            ct
        );

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PlatformAdmin_CanCreateProduct()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        IntegrationAuthHelper.Authenticate(client);

        var response = await client.PostAsJsonAsync(
            "/api/v1/products",
            new
            {
                Items = new[]
                {
                    new
                    {
                        Name = "Admin Product",
                        Description = "Should succeed",
                        Price = 20.00,
                    },
                },
            },
            ct
        );

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PlatformAdmin_CanGetUsers()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        IntegrationAuthHelper.Authenticate(client);

        var response = await client.GetAsync("/api/v1/users", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task User_CannotDeleteProduct()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        IntegrationAuthHelper.AuthenticateAsUser(client);

        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/products")
        {
            Content = JsonContent.Create(new { Ids = new[] { Guid.NewGuid() } }),
        };
        var response = await client.SendAsync(deleteRequest, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TenantAdmin_CanReadUsers()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        IntegrationAuthHelper.AuthenticateAsTenantAdmin(client);

        var response = await client.GetAsync("/api/v1/users", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task User_CanCreateProductReview()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        IntegrationAuthHelper.AuthenticateAsUser(client);

        // First create a product as PlatformAdmin
        var adminClient = _factory.CreateClient();
        IntegrationAuthHelper.Authenticate(adminClient);
        var productName = $"Review Target-{Guid.NewGuid():N}";
        var productResponse = await adminClient.PostAsJsonAsync(
            "/api/v1/products",
            new
            {
                Items = new[]
                {
                    new
                    {
                        Name = productName,
                        Description = "For review",
                        Price = 5.00,
                    },
                },
            },
            ct
        );
        var productBody = await productResponse.Content.ReadAsStringAsync(ct);
        productResponse.StatusCode.ShouldBe(HttpStatusCode.OK, productBody);
        var productBatch = JsonSerializer.Deserialize<BatchResponse>(
            productBody,
            TestJsonOptions.CaseInsensitive
        );
        productBatch.ShouldNotBeNull();
        productBatch!.Failures.ShouldBeEmpty();
        var productId = await ResolveProductIdAsync(adminClient, productName, 5.00m, ct);

        // Then create a review as User
        var response = await client.PostAsJsonAsync(
            "/api/v1/productreviews",
            new
            {
                ProductId = productId,
                Rating = 5,
                Comment = "Great!",
            },
            ct
        );

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private static async Task<Guid> ResolveProductIdAsync(
        HttpClient client,
        string name,
        decimal price,
        CancellationToken ct
    )
    {
        var response = await client.GetAsync(
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
