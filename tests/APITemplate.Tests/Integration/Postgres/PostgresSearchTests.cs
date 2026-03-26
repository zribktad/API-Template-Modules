using System.Net;
using System.Net.Http.Json;
using APITemplate.Application.Features.Product;
using APITemplate.Domain.Entities;
using APITemplate.Infrastructure.Persistence;
using APITemplate.Tests.Integration.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Postgres;

public sealed class PostgresSearchTests(SharedPostgresContainer postgres) : PostgresTestBase(postgres)
{
    [Fact]
    public async Task ProductSearch_FullTextAndFacets_ReturnTenantScopedResults()
    {
        var ct = TestContext.Current.CancellationToken;
        var username = $"search-{Guid.NewGuid():N}";
        var (tenant, _) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            username,
            $"{username}@example.com",
            ct: ct);

        var otherTenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Code = $"search-other-{Guid.NewGuid():N}",
            Name = "Other Search Tenant"
        };

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var electronics = new Category
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Name = "Electronics"
            };

            var books = new Category
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Name = "Books"
            };

            var otherCategory = new Category
            {
                Id = Guid.NewGuid(),
                TenantId = otherTenant.Id,
                Name = "Electronics"
            };

            db.Tenants.Add(otherTenant);
            db.Categories.AddRange(electronics, books, otherCategory);
            db.Products.AddRange(
                new Product
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    Name = "Wireless Mouse",
                    Description = "Silent office mouse",
                    Price = 30m,
                    CategoryId = electronics.Id
                },
                new Product
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    Name = "Wireless Keyboard",
                    Description = "Mechanical office keyboard",
                    Price = 80m,
                    CategoryId = electronics.Id
                },
                new Product
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    Name = "Fantasy Novel",
                    Description = "Epic dragon story",
                    Price = 15m,
                    CategoryId = books.Id
                },
                new Product
                {
                    Id = Guid.NewGuid(),
                    TenantId = otherTenant.Id,
                    Name = "Wireless Speaker",
                    Description = "Other tenant item",
                    Price = 120m,
                    CategoryId = otherCategory.Id
                });

            await db.SaveChangesAsync(ct);
        }

        IntegrationAuthHelper.Authenticate(_client, tenantId: tenant.Id, username: username, role: Domain.Enums.UserRole.User);

        var response = await _client.GetAsync("/api/v1/products?query=wireless", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ProductsResponse>(TestJsonOptions.CaseInsensitive, ct);
        payload.ShouldNotBeNull();

        payload!.Page.Items.Count().ShouldBe(2);
        payload.Page.Items.Select(item => item.Name).ShouldBe(["Wireless Mouse", "Wireless Keyboard"], ignoreOrder: true);
        payload.Facets.Categories.Count.ShouldBe(1);
        payload.Facets.Categories.Single().CategoryName.ShouldBe("Electronics");
        payload.Facets.Categories.Single().Count.ShouldBe(2);
        payload.Facets.PriceBuckets.Single(bucket => bucket.Label == "0 - 50").Count.ShouldBe(1);
        payload.Facets.PriceBuckets.Single(bucket => bucket.Label == "50 - 100").Count.ShouldBe(1);
    }

    [Fact]
    public async Task GraphQL_SearchQueries_UsePostgresFullText()
    {
        var ct = TestContext.Current.CancellationToken;
        var graphql = new GraphQLTestHelper(_client);
        var username = $"graphql-search-{Guid.NewGuid():N}";
        var (tenant, _) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            username,
            $"{username}@example.com",
            ct: ct);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.Categories.AddRange(
                new Category
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    Name = "Office Supplies",
                    Description = "Desk organization"
                },
                new Category
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    Name = "Kitchen Goods",
                    Description = "Cookware"
                });

            db.Products.AddRange(
                new Product
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    Name = "Wireless Charger",
                    Description = "Fast charging pad",
                    Price = 40m
                },
                new Product
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    Name = "Paper Notebook",
                    Description = "Meeting notes",
                    Price = 12m
                });

            await db.SaveChangesAsync(ct);
        }

        IntegrationAuthHelper.Authenticate(_client, tenantId: tenant.Id, username: username, role: Domain.Enums.UserRole.User);

        var productsQuery = new
        {
            query = @"
                query($input: ProductQueryInput) {
                    products(input: $input) {
                        page {
                            items { id name price }
                            totalCount
                        }
                        facets { priceBuckets { label count } }
                    }
                }",
            variables = new
            {
                input = new
                {
                    query = "wireless",
                    pageNumber = 1,
                    pageSize = 10
                }
            }
        };

        var products = await graphql.ReadRequiredGraphQLFieldAsync<ProductsData, ProductPage>(
            await graphql.PostAsync(productsQuery),
            data => data.Products,
            "products");

        products.Page.Items.Count.ShouldBe(1);
        products.Page.Items[0].Name.ShouldBe("Wireless Charger");

        var categoriesQuery = new
        {
            query = @"
                query($input: CategoryQueryInput) {
                    categories(input: $input) {
                        page {
                            items { id name }
                            totalCount
                        }
                    }
                }",
            variables = new
            {
                input = new
                {
                    query = "office",
                    pageNumber = 1,
                    pageSize = 10
                }
            }
        };

        var categories = await graphql.ReadRequiredGraphQLFieldAsync<CategoriesData, CategoryPage>(
            await graphql.PostAsync(categoriesQuery),
            data => data.Categories,
            "categories");

        categories.Page.Items.Count.ShouldBe(1);
        categories.Page.Items[0].Name.ShouldBe("Office Supplies");
        categories.Page.TotalCount.ShouldBe(1);
    }
}
