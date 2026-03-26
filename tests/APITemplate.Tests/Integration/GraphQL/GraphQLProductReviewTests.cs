using APITemplate.Tests.Integration.Helpers;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.GraphQL;

public class GraphQLProductReviewTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly GraphQLTestHelper _graphql;
    private readonly Guid _tenantId = Guid.NewGuid();

    public GraphQLProductReviewTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _graphql = new GraphQLTestHelper(_client);
    }

    [Fact]
    public async Task GraphQL_CreateProductReview_ReturnsNewReview()
    {
        var ct = TestContext.Current.CancellationToken;
        var userId = IntegrationAuthHelper.AuthenticateAndGetUserId(_client, tenantId: _tenantId);
        var productId = await _graphql.CreateProductAsync("Review Target Product", 19.99m);

        var mutation = new
        {
            query = @"
                mutation($input: CreateProductReviewRequestInput!) {
                    createProductReview(input: $input) {
                        id
                        userId
                        rating
                        productId
                    }
                }",
            variables = new
            {
                input = new
                {
                    productId,
                    comment = "Tested via GraphQL",
                    rating = 4,
                },
            },
        };

        var response = await _graphql.PostAsync(mutation);
        var createProductReview = await _graphql.ReadRequiredGraphQLFieldAsync<
            CreateProductReviewData,
            ProductReviewItem
        >(response, data => data.CreateProductReview, "createProductReview");
        createProductReview.UserId.ShouldBe(userId);
        createProductReview.Rating.ShouldBe(4);
        createProductReview.ProductId.ShouldBe(productId);
    }

    [Fact]
    public async Task GraphQL_GetReviews_ReturnsEmptyOrPopulatedList()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var query = new
        {
            query = "{ reviews { page { items { id userId rating } totalCount pageNumber pageSize } } }",
        };

        var response = await _graphql.PostAsync(query);
        var reviews = await _graphql.ReadRequiredGraphQLFieldAsync<ReviewsData, ProductReviewPage>(
            response,
            data => data.Reviews,
            "reviews"
        );
        reviews.Page.Items.Count.ShouldBeGreaterThanOrEqualTo(0);
        reviews.Page.PageNumber.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task GraphQL_GetReviewsByProductId_ReturnsReviewsForProduct()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);
        var productId = await _graphql.CreateProductAsync("Product With Reviews", 29.99m);

        var createMutation = new
        {
            query = @"
                mutation($input: CreateProductReviewRequestInput!) {
                    createProductReview(input: $input) { id }
                }",
            variables = new { input = new { productId, rating = 3 } },
        };
        await _graphql.PostAsync(createMutation);

        var query = new
        {
            query = $@"{{ reviewsByProductId(productId: ""{productId}"", pageNumber: 1, pageSize: 20) {{ page {{ items {{ id userId rating }} totalCount }} }} }}",
        };

        var response = await _graphql.PostAsync(query);
        var reviewsByProductId = await _graphql.ReadRequiredGraphQLFieldAsync<
            ReviewsByProductIdData,
            ProductReviewPage
        >(response, data => data.ReviewsByProductId, "reviewsByProductId");
        reviewsByProductId.Page.Items.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GraphQL_GetReviews_WithFilterSortAndPaging_ReturnsExpectedOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);
        var productId = await _graphql.CreateProductAsync($"SortTarget-{Guid.NewGuid():N}", 15m);

        await _graphql.CreateReviewAsync(productId, 2);
        await _graphql.CreateReviewAsync(productId, 5);
        await _graphql.CreateReviewAsync(productId, 4);

        var query = new
        {
            query = @"
                query($input: ProductReviewQueryInput) {
                    reviews(input: $input) {
                        page {
                            items { id userId rating productId }
                            totalCount
                            pageNumber
                            pageSize
                        }
                    }
                }",
            variables = new
            {
                input = new
                {
                    productId,
                    sortBy = "rating",
                    sortDirection = "desc",
                    pageNumber = 1,
                    pageSize = 2,
                },
            },
        };

        var response = await _graphql.PostAsync(query);
        var reviews = await _graphql.ReadRequiredGraphQLFieldAsync<ReviewsData, ProductReviewPage>(
            response,
            data => data.Reviews,
            "reviews"
        );
        var items = reviews.Page.Items;

        items.Count.ShouldBe(2);
        items[0].Rating.ShouldBeGreaterThanOrEqualTo(items[1].Rating);
        reviews.Page.TotalCount.ShouldBeGreaterThanOrEqualTo(3);
        reviews.Page.PageNumber.ShouldBe(1);
        reviews.Page.PageSize.ShouldBe(2);
    }

    [Fact]
    public async Task GraphQL_DeleteProductReview_ReturnsTrue()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);
        var productId = await _graphql.CreateProductAsync(
            "Product To Review Then Delete Review",
            9.99m
        );

        var createMutation = new
        {
            query = @"
                mutation($input: CreateProductReviewRequestInput!) {
                    createProductReview(input: $input) { id }
                }",
            variables = new { input = new { productId, rating = 2 } },
        };

        var createResponse = await _graphql.PostAsync(createMutation);
        var createdReview = await _graphql.ReadRequiredGraphQLFieldAsync<
            CreateProductReviewData,
            ProductReviewItem
        >(createResponse, data => data.CreateProductReview, "createProductReview");
        var reviewId = createdReview.Id;

        var deleteMutation = new
        {
            query = $@"mutation {{ deleteProductReview(id: ""{reviewId}"") }}",
        };

        var deleteResponse = await _graphql.PostAsync(deleteMutation);
        var deleteResult = await _graphql.ReadGraphQLResponseAsync<DeleteProductReviewData>(
            deleteResponse
        );
        deleteResult.DeleteProductReview.ShouldBeTrue();
    }
}
