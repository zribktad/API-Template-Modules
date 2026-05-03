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
        // Category -> products -> categories -> products -> categories -> products
        string deepQuery =
            @"
        query {
          categories {
            page {
              items {
                products {
                  page {
                    items {
                      categories {
                        page {
                          items {
                            id
                          }
                        }
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
