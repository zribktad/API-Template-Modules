using System.Net;
using System.Text;
using System.Text.Json;
using APITemplate.Tests.Integration.GraphQL;
using Shouldly;
using Xunit;
using Xunit.Sdk;

namespace APITemplate.Tests.Integration.Helpers;

internal sealed class GraphQLTestHelper
{
    private readonly HttpClient _client;

    internal GraphQLTestHelper(HttpClient client)
    {
        _client = client;
    }

    internal async Task<HttpResponseMessage> PostAsync(object query)
    {
        var ct = TestContext.Current.CancellationToken;
        var json = JsonSerializer.Serialize(query);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _client.PostAsync("/graphql", content, ct);
    }

    internal async Task<Guid> CreateProductAsync(string name, decimal price)
    {
        var mutation = new
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
            variables = new { input = new { items = new[] { new { name, price } } } },
        };

        var response = await PostAsync(mutation);
        var batch = await ReadRequiredGraphQLFieldAsync<CreateProductsData, GraphQLBatchResult>(
            response,
            data => data.CreateProducts,
            "createProducts"
        );
        batch.SuccessCount.ShouldBe(1);
        batch.FailureCount.ShouldBe(0);
        return await GetProductIdByNameAndPriceAsync(name, price);
    }

    /// <summary>Resolves a product id from the paginated list (e.g. after a batch create that does not return entity ids).</summary>
    internal async Task<Guid> GetProductIdByNameAndPriceAsync(string name, decimal price)
    {
        var lookup = new
        {
            query = """
                query($name: String!, $min: Decimal!, $max: Decimal!) {
                    products(input: {
                        name: $name
                        minPrice: $min
                        maxPrice: $max
                        pageNumber: 1
                        pageSize: 25
                        sortBy: "createdAt"
                        sortDirection: "desc"
                    }) {
                        page { items { id name price } }
                    }
                }
                """,
            variables = new
            {
                name,
                min = price,
                max = price,
            },
        };

        var lookupResponse = await PostAsync(lookup);
        var page = await ReadRequiredGraphQLFieldAsync<ProductsData, ProductPage>(
            lookupResponse,
            data => data.Products,
            "products"
        );
        var match = page.Page.Items.FirstOrDefault(i => i.Name == name && i.Price == price);
        match.ShouldNotBeNull($"Expected a product named '{name}' with price {price}.");
        return match!.Id;
    }

    internal async Task<Guid> CreateReviewAsync(Guid productId, int rating)
    {
        var mutation = new
        {
            query = """
                mutation($input: CreateProductReviewRequestInput!) {
                    createProductReview(input: $input) { id }
                }
                """,
            variables = new { input = new { productId, rating } },
        };

        var response = await PostAsync(mutation);
        var result = await ReadRequiredGraphQLFieldAsync<
            CreateProductReviewData,
            ProductReviewItem
        >(response, data => data.CreateProductReview, "createProductReview");
        return result.Id;
    }

    internal async Task<TData> ReadGraphQLResponseAsync<TData>(HttpResponseMessage response)
    {
        var (payload, _) = await ReadGraphQLPayloadAsync<TData>(response);
        return payload;
    }

    internal async Task<TValue> ReadRequiredGraphQLFieldAsync<TData, TValue>(
        HttpResponseMessage response,
        Func<TData, TValue?> selector,
        string fieldName
    )
        where TValue : class
    {
        var (payload, body) = await ReadGraphQLPayloadAsync<TData>(response);
        var value = selector(payload);
        if (value is null)
        {
            throw new XunitException(
                $"GraphQL response contained a null '{fieldName}' field. Response body: {body}"
            );
        }

        return value;
    }

    private async Task<(TData Payload, string Body)> ReadGraphQLPayloadAsync<TData>(
        HttpResponseMessage response
    )
    {
        var ct = TestContext.Current.CancellationToken;
        var body = await response.Content.ReadAsStringAsync(ct);

        response.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            $"GraphQL request returned HTTP {(int)response.StatusCode}. Response body: {body}"
        );

        GraphQLResponse<TData>? payload;
        try
        {
            payload = JsonSerializer.Deserialize<GraphQLResponse<TData>>(
                body,
                GraphQLJsonOptions.Default
            );
        }
        catch (JsonException ex)
        {
            throw new XunitException(
                $"GraphQL response could not be deserialized into {typeof(TData).Name}. "
                    + $"Response body: {body}{Environment.NewLine}{ex}"
            );
        }

        if (payload is null)
            throw new XunitException(
                $"GraphQL response was null after deserialization. Response body: {body}"
            );

        if (payload.Errors is { Count: > 0 })
        {
            var errors = string.Join(
                Environment.NewLine,
                payload.Errors.Select(error => $"- {error.Message}")
            );
            throw new XunitException(
                $"GraphQL response contained errors:{Environment.NewLine}{errors}{Environment.NewLine}Response body: {body}"
            );
        }

        if (payload.Data is null)
            throw new XunitException(
                $"GraphQL response did not contain a data payload. Response body: {body}"
            );

        return (payload.Data, body);
    }
}
