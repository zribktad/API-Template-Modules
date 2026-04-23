using System.Net;
using System.Text;
using Identity.Auth.Security;
using SharedKernel.Contracts.Security;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Smoke;

[Collection("Smoke")]
[Trait("Category", "Smoke")]
public sealed class ProductAndReviewSmokeTests : IAsyncLifetime
{
    private const string ServiceAccountUsername =
        $"{AuthConstants.Claims.ServiceAccountUsernamePrefix}smoke-products";
    private readonly CustomWebApplicationFactory _factory;
    private Guid _tenantId;

    private HttpClient Client => field ??= _factory.CreateClient();

    public ProductAndReviewSmokeTests(CustomWebApplicationFactory factory) => _factory = factory;

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (tenant, _) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            username: $"smoke-product-{suffix}",
            email: $"smoke-product-{suffix}@example.com",
            ct: ct
        );
        _tenantId = tenant.Id;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task GetProducts_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(
            Client,
            tenantId: _tenantId,
            username: ServiceAccountUsername,
            permissions: [Permission.Products.Read]
        );
        var response = await Client.GetAsync("/api/v1/products", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetCategories_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(
            Client,
            tenantId: _tenantId,
            username: ServiceAccountUsername,
            permissions: [Permission.Categories.Read]
        );
        var response = await Client.GetAsync("/api/v1/categories", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetProductData_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(
            Client,
            tenantId: _tenantId,
            username: ServiceAccountUsername,
            permissions: [Permission.ProductData.Read]
        );
        var response = await Client.GetAsync("/api/v1/product-data", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetProductReviews_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(
            Client,
            tenantId: _tenantId,
            username: ServiceAccountUsername,
            permissions: [Permission.ProductReviews.Read]
        );
        var response = await Client.GetAsync(
            "/api/v1/product-reviews?pageNumber=1&pageSize=20",
            ct
        );
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GraphQL_BasicRequest_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(
            Client,
            tenantId: _tenantId,
            username: ServiceAccountUsername,
            permissions: [Permission.Products.Read]
        );
        var content = new StringContent(
            """{"query":"{ __typename }"}""",
            Encoding.UTF8,
            "application/json"
        );
        var response = await Client.PostAsync("/graphql", content, ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
