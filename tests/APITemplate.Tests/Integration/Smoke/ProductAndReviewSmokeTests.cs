using System.Net;
using System.Text;
using SharedKernel.Contracts.Security;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Smoke;

[Trait("Category", "Integration.Docker")]
public sealed class ProductAndReviewSmokeTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private HttpClient? _client;

    private HttpClient Client => _client ??= _factory.CreateClient();

    public ProductAndReviewSmokeTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetProducts_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(Client, permissions: [Permission.Products.Read]);
        var response = await Client.GetAsync("/api/v1/products", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetCategories_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(Client, permissions: [Permission.Categories.Read]);
        var response = await Client.GetAsync("/api/v1/categories", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetProductData_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(Client, permissions: [Permission.ProductData.Read]);
        var response = await Client.GetAsync("/api/v1/product-data", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetProductReviews_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(Client, permissions: [Permission.ProductReviews.Read]);
        var response = await Client.GetAsync("/api/v1/product-reviews", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GraphQL_Introspection_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(Client, permissions: [Permission.Products.Read]);
        var content = new StringContent(
            """{"query":"{ __typename }"}""",
            Encoding.UTF8,
            "application/json"
        );
        var response = await Client.PostAsync("/graphql", content, ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
