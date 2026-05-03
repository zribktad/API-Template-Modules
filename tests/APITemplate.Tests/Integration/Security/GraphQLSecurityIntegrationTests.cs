using System.Net;
using System.Text;
using System.Text.Json;
using APITemplate.Tests.Integration.Helpers;
using Identity.Auth.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using SharedKernel.Application.Constants;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Security;

[Trait("Category", "Integration")]
[Trait("Docker", "true")]
[Collection(IntegrationCollectionNames.HttpStateful)]
public sealed class GraphQLSecurityIntegrationTests
{
    private readonly HttpClient _client;

    public GraphQLSecurityIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
    }

    [Fact]
    public async Task GraphQL_DepthLimit_WhenExceeded_ReturnsError()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(
            _client,
            username: $"{AuthConstants.Claims.ServiceAccountUsernamePrefix}graphql-security"
        );

        // Construct a deeply nested query (depth > 5)
        // categories (1) -> page (2) -> items (3) -> products (4) -> page (5) -> items (6) -> category (7) -> id (8)
        string deepQuery =
            @"
        query {
          categories {
            page {
              items {
                products {
                  page {
                    items {
                      category {
                        id
                      }
                    }
                  }
                }
              }
            }
          }
        }";

        var response = await ExecuteGraphQLQueryAsync(deepQuery, ct);
        string responseBody = await response.Content.ReadAsStringAsync(ct);

        using JsonDocument doc = JsonDocument.Parse(responseBody);
        doc.RootElement.TryGetProperty("errors", out JsonElement errors).ShouldBeTrue(responseBody);
        errors[0]
            .GetProperty("message")
            .GetString()
            .ShouldNotBeNull()
            .ToLowerInvariant()
            .ShouldContain("depth");
    }

    [Fact]
    public async Task GraphQL_FieldCostLimit_WhenExceeded_ReturnsError()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(
            _client,
            username: $"{AuthConstants.Claims.ServiceAccountUsernamePrefix}graphql-security"
        );

        // Construct a query with very high cost (> 1000)
        // We use aliases to repeat a field many times to bloat the complexity cost.
        // We keep the total field count below 2048 to avoid the parser limit.
        StringBuilder queryBuilder = new();
        queryBuilder.AppendLine("query {");
        for (int i = 0; i < 200; i++)
        {
            queryBuilder.AppendLine(
                $"  c{i}: categories {{ page {{ items {{ products {{ page {{ totalCount }} }} }} }} }}"
            );
        }
        queryBuilder.AppendLine("}");

        var response = await ExecuteGraphQLQueryAsync(queryBuilder.ToString(), ct);
        string responseBody = await response.Content.ReadAsStringAsync(ct);

        using JsonDocument doc = JsonDocument.Parse(responseBody);
        doc.RootElement.TryGetProperty("errors", out JsonElement errors).ShouldBeTrue(responseBody);
        errors[0]
            .GetProperty("message")
            .GetString()
            .ShouldNotBeNull()
            .ToLowerInvariant()
            .ShouldContain("cost");
    }

    [Fact]
    public async Task GraphQL_PageSizeLimit_WhenExceeded_ReturnsError()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(
            _client,
            username: $"{AuthConstants.Claims.ServiceAccountUsernamePrefix}graphql-security"
        );

        string largePageQuery =
            @"
        query {
          products(input: { pageSize: 101, pageNumber: 1 }) {
            page {
              items {
                id
              }
            }
          }
        }";

        var response = await ExecuteGraphQLQueryAsync(largePageQuery, ct);
        string responseBody = await response.Content.ReadAsStringAsync(ct);

        using JsonDocument doc = JsonDocument.Parse(responseBody);
        doc.RootElement.TryGetProperty("errors", out JsonElement errors).ShouldBeTrue(responseBody);
        errors[0].GetProperty("message").GetString().ShouldNotBeNull().ShouldContain("100");
    }

    private async Task<HttpResponseMessage> ExecuteGraphQLQueryAsync(
        string query,
        CancellationToken ct
    )
    {
        string requestBody = JsonSerializer.Serialize(new { query });
        using StringContent content = new(requestBody, Encoding.UTF8, "application/json");
        return await _client.PostAsync("/graphql", content, ct);
    }
}
