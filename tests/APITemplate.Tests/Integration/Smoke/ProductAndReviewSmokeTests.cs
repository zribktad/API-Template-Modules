using System.Net;
using System.Net.Http.Json;
using SharedKernel.Contracts.Security;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Smoke;

[Collection("Smoke")]
[Trait("Category", "Smoke")]
public sealed class ProductAndReviewSmokeTests : SmokeTestBase
{
    public ProductAndReviewSmokeTests(CustomWebApplicationFactory factory)
        : base(factory) { }

    protected override string UsernamePrefix => "smoke-product";

    [Fact]
    public async Task GetProducts_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        AuthenticateAsServiceAccount(Permission.Products.Read);
        var response = await Client.GetAsync("/api/v1/products", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetCategories_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        AuthenticateAsServiceAccount(Permission.Categories.Read);
        var response = await Client.GetAsync("/api/v1/categories", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetProductData_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        AuthenticateAsServiceAccount(Permission.ProductData.Read);
        var response = await Client.GetAsync("/api/v1/product-data", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetProductReviews_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        AuthenticateAsServiceAccount(Permission.ProductReviews.Read);
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
        AuthenticateAsServiceAccount(Permission.Products.Read);
        var response = await Client.PostAsJsonAsync(
            "/graphql",
            new { query = "{ __typename }" },
            ct
        );
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
