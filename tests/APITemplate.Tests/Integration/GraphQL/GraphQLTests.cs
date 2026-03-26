using System.Net;
using System.Net.Http.Json;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using APITemplate.Tests.Integration.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.GraphQL;

public class GraphQLTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly GraphQLTestHelper _graphql;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Mock<IProductDataRepository> _productDataRepositoryMock;

    public GraphQLTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _graphql = new GraphQLTestHelper(_client);
        _productDataRepositoryMock = factory.Services.GetRequiredService<
            Mock<IProductDataRepository>
        >();
        _productDataRepositoryMock.Reset();
    }

    [Fact]
    public async Task GraphQL_GetProducts_ReturnsEmptyList()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var query = new
        {
            query = "{ products { page { items { id name price } totalCount pageNumber pageSize } facets { categories { categoryName count } priceBuckets { label count } } } }",
        };

        var response = await _graphql.PostAsync(query);
        var products = await _graphql.ReadRequiredGraphQLFieldAsync<ProductsData, ProductPage>(
            response,
            data => data.Products,
            "products"
        );
        products.Page.Items.Count.ShouldBeGreaterThanOrEqualTo(0);
        products.Page.PageNumber.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task GraphQL_CreateProducts_WithSingleItem_ReturnsBatchResult()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var query = new
        {
            query = """
                mutation($input: CreateProductsRequestInput!) {
                    createProducts(input: $input) {
                        successCount
                        failureCount
                        failures { index id errors }
                    }
                }
                """,
            variables = new
            {
                input = new
                {
                    items = new[]
                    {
                        new
                        {
                            name = "GraphQL Product",
                            description = "Created via GraphQL",
                            price = 49.99,
                        },
                    },
                },
            },
        };

        var response = await _graphql.PostAsync(query);
        var createProducts = await _graphql.ReadRequiredGraphQLFieldAsync<
            CreateProductsData,
            GraphQLBatchResult
        >(response, data => data.CreateProducts, "createProducts");
        createProducts.SuccessCount.ShouldBe(1);
        createProducts.FailureCount.ShouldBe(0);
        createProducts.Failures.ShouldBeEmpty();
    }

    [Fact]
    public async Task GraphQL_CreateProduct_GeneratesId()
    {
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var productId = await _graphql.CreateProductAsync("GraphQL Product Generated Id", 19.99m);
        productId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task GraphQL_CreateProduct_WithProductDataIds_ReturnsIds()
    {
        var productDataId = Guid.NewGuid();
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        _productDataRepositoryMock
            .Setup(r =>
                r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([new ImageProductData { Id = productDataId, Title = "Image" }]);
        var query = new
        {
            query = """
                mutation($input: CreateProductsRequestInput!) {
                    createProducts(input: $input) {
                        successCount
                        failureCount
                        failures { index id errors }
                    }
                }
                """,
            variables = new
            {
                input = new
                {
                    items = new[]
                    {
                        new
                        {
                            name = "GraphQL Product With Data",
                            price = 49.99,
                            productDataIds = new[] { productDataId },
                        },
                    },
                },
            },
        };

        var response = await _graphql.PostAsync(query);
        var batch = await _graphql.ReadRequiredGraphQLFieldAsync<
            CreateProductsData,
            GraphQLBatchResult
        >(response, data => data.CreateProducts, "createProducts");
        batch.SuccessCount.ShouldBe(1);
        batch.FailureCount.ShouldBe(0);
        batch.Failures.ShouldBeEmpty();

        var createdId = await _graphql.GetProductIdByNameAndPriceAsync(
            "GraphQL Product With Data",
            49.99m
        );
        var getQuery = new
        {
            query = $@"{{ productById(id: ""{createdId}"") {{ id productDataIds }} }}",
        };
        var getResponse = await _graphql.PostAsync(getQuery);
        var loaded = await _graphql.ReadGraphQLResponseAsync<ProductByIdData>(getResponse);
        loaded.ProductById.ShouldNotBeNull();
        loaded.ProductById!.ProductDataIds.ShouldBe([productDataId]);
    }

    [Fact]
    public async Task GraphQL_GetProductById_WhenExists_ReturnsProduct()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var productId = await _graphql.CreateProductAsync("Findable Product", 10.0m);

        var getQuery = new
        {
            query = $@"{{ productById(id: ""{productId}"") {{ id name productDataIds }} }}",
        };

        var getResponse = await _graphql.PostAsync(getQuery);
        var getResult = await _graphql.ReadGraphQLResponseAsync<ProductByIdData>(getResponse);
        getResult.ProductById.ShouldNotBeNull();
        getResult.ProductById.Name.ShouldBe("Findable Product");
    }

    [Fact]
    public async Task GraphQL_DeleteProduct_ReturnsTrue()
    {
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var productId = await _graphql.CreateProductAsync("To Delete", 5.0m);

        var deleteQuery = new { query = $@"mutation {{ deleteProduct(id: ""{productId}"") }}" };

        var deleteResponse = await _graphql.PostAsync(deleteQuery);
        var deleteResult = await _graphql.ReadGraphQLResponseAsync<DeleteProductData>(
            deleteResponse
        );
        deleteResult.DeleteProduct.ShouldBeTrue();
    }

    [Fact]
    public async Task GraphQL_CreateProducts_ReturnsBatchResult()
    {
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var query = new
        {
            query = """
                mutation($input: CreateProductsRequestInput!) {
                    createProducts(input: $input) {
                        successCount
                        failureCount
                        failures { index id errors }
                    }
                }
                """,
            variables = new
            {
                input = new
                {
                    items = new[]
                    {
                        new { name = "GraphQL Batch P1", price = 11.0 },
                        new { name = "GraphQL Batch P2", price = 22.0 },
                    },
                },
            },
        };

        var response = await _graphql.PostAsync(query);
        var batch = await _graphql.ReadRequiredGraphQLFieldAsync<
            CreateProductsData,
            GraphQLBatchResult
        >(response, data => data.CreateProducts, "createProducts");
        batch.SuccessCount.ShouldBe(2);
        batch.FailureCount.ShouldBe(0);
        batch.Failures.ShouldBeEmpty();
    }

    [Fact]
    public async Task GraphQL_DeleteProducts_ReturnsBatchResult()
    {
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var productId = await _graphql.CreateProductAsync("To Batch Delete", 7.0m);
        var query = new
        {
            query = """
                mutation($input: BatchDeleteRequestInput!) {
                    deleteProducts(input: $input) {
                        successCount
                        failureCount
                        failures { index id errors }
                    }
                }
                """,
            variables = new { input = new { ids = new[] { productId } } },
        };

        var response = await _graphql.PostAsync(query);
        var batch = await _graphql.ReadRequiredGraphQLFieldAsync<
            DeleteProductsData,
            GraphQLBatchResult
        >(response, data => data.DeleteProducts, "deleteProducts");
        batch.SuccessCount.ShouldBe(1);
        batch.FailureCount.ShouldBe(0);
        batch.Failures.ShouldBeEmpty();
    }

    [Fact]
    public async Task GraphQL_GetProducts_WithFilterSortAndPaging_ReturnsExpectedOrderAndSlice()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var prefix = $"sort-{Guid.NewGuid():N}";
        await _graphql.CreateProductAsync($"{prefix}-A", 30m);
        await _graphql.CreateProductAsync($"{prefix}-B", 10m);
        await _graphql.CreateProductAsync($"{prefix}-C", 20m);

        var query = new
        {
            query = @"
                query($input: ProductQueryInput) {
                    products(input: $input) {
                        page {
                            items { id name price }
                            totalCount
                            pageNumber
                            pageSize
                        }
                        facets {
                            categories { categoryId categoryName count }
                            priceBuckets { label minPrice maxPrice count }
                        }
                    }
                }",
            variables = new
            {
                input = new
                {
                    name = prefix,
                    sortBy = "price",
                    sortDirection = "asc",
                    pageNumber = 1,
                    pageSize = 2,
                },
            },
        };

        var response = await _graphql.PostAsync(query);
        var products = await _graphql.ReadRequiredGraphQLFieldAsync<ProductsData, ProductPage>(
            response,
            data => data.Products,
            "products"
        );
        var items = products.Page.Items;

        items.Count.ShouldBe(2);
        items[0].Price.ShouldBeLessThanOrEqualTo(items[1].Price);
        products.Page.TotalCount.ShouldBeGreaterThanOrEqualTo(3);
        products.Page.PageNumber.ShouldBe(1);
        products.Page.PageSize.ShouldBe(2);
        products.Facets.ShouldNotBeNull();
    }

    [Fact]
    public async Task GraphQL_ProductReviewsField_UsesBatchResolverAndReturnsReviewsPerProduct()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var prefix = $"dl-{Guid.NewGuid():N}";
        var p1 = await _graphql.CreateProductAsync($"{prefix}-P1", 11m);
        var p2 = await _graphql.CreateProductAsync($"{prefix}-P2", 22m);

        await _graphql.CreateReviewAsync(p1, 5);
        await _graphql.CreateReviewAsync(p1, 4);
        await _graphql.CreateReviewAsync(p2, 3);

        var query = new
        {
            query = @"
                query($input: ProductQueryInput) {
                    products(input: $input) {
                        page {
                            items {
                                id
                                name
                                price
                                reviews { id rating productId }
                            }
                            totalCount
                            pageNumber
                            pageSize
                        }
                        facets { categories { categoryName count } }
                    }
                }",
            variables = new
            {
                input = new
                {
                    name = prefix,
                    pageNumber = 1,
                    pageSize = 10,
                },
            },
        };

        var response = await _graphql.PostAsync(query);
        var products = await _graphql.ReadRequiredGraphQLFieldAsync<
            ProductsWithReviewsData,
            ProductWithReviewsPage
        >(response, data => data.Products, "products");
        var items = products.Page.Items;

        items.Count.ShouldBeGreaterThanOrEqualTo(2);
        items.ShouldContain(x => x.Id == p1 && x.Reviews.Count >= 2);
        items.ShouldContain(x => x.Id == p2 && x.Reviews.Count >= 1);
    }

    [Fact]
    public async Task GraphQL_GetProducts_WithFacets_ReturnsFacetPayload()
    {
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        await _graphql.CreateProductAsync("Wireless Charger", 40m);
        await _graphql.CreateProductAsync("Wireless Earbuds", 90m);
        await _graphql.CreateProductAsync("Paper Notebook", 12m);

        var query = new
        {
            query = @"
                query($input: ProductQueryInput) {
                    products(input: $input) {
                        page {
                            items { id name price }
                            totalCount
                            pageNumber
                            pageSize
                        }
                        facets {
                            categories { categoryName count }
                            priceBuckets { label count }
                        }
                    }
                }",
            variables = new
            {
                input = new
                {
                    pageNumber = 1,
                    pageSize = 10,
                    sortBy = "price",
                    sortDirection = "asc",
                },
            },
        };

        var response = await _graphql.PostAsync(query);
        var products = await _graphql.ReadRequiredGraphQLFieldAsync<ProductsData, ProductPage>(
            response,
            data => data.Products,
            "products"
        );

        products.Page.Items.Count.ShouldBe(3);
        products.Facets.ShouldNotBeNull();
        products.Facets!.PriceBuckets.ShouldContain(bucket =>
            bucket.Label == "0 - 50" && bucket.Count >= 2
        );
    }

    [Fact]
    public async Task GraphQL_GetCategories_ReturnsPagedCategories()
    {
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var officeResponse = await _client.PostAsJsonAsync(
            "/api/v1/categories",
            new
            {
                Items = new[]
                {
                    new { Name = "Office Supplies", Description = "Desk organization" },
                },
            },
            TestContext.Current.CancellationToken
        );
        officeResponse.EnsureSuccessStatusCode();

        var kitchenResponse = await _client.PostAsJsonAsync(
            "/api/v1/categories",
            new
            {
                Items = new[]
                {
                    new { Name = "Kitchen Goods", Description = "Cookware and utensils" },
                },
            },
            TestContext.Current.CancellationToken
        );
        kitchenResponse.EnsureSuccessStatusCode();

        var query = new
        {
            query = @"
                query($input: CategoryQueryInput) {
                    categories(input: $input) {
                        page {
                            items { id name description }
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
                    pageNumber = 1,
                    pageSize = 10,
                    sortBy = "name",
                    sortDirection = "asc",
                },
            },
        };

        var response = await _graphql.PostAsync(query);
        var categories = await _graphql.ReadRequiredGraphQLFieldAsync<CategoriesData, CategoryPage>(
            response,
            data => data.Categories,
            "categories"
        );

        categories.Page.Items.Count.ShouldBeGreaterThanOrEqualTo(2);
        categories.Page.TotalCount.ShouldBeGreaterThanOrEqualTo(2);
    }
}
