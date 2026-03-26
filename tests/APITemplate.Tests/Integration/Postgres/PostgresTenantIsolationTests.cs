using System.Net;
using System.Net.Http.Json;
using APITemplate.Application.Features.Product;
using APITemplate.Application.Features.ProductReview;
using APITemplate.Domain.Entities;
using APITemplate.Infrastructure.Persistence;
using APITemplate.Tests.Integration.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Postgres;

public sealed class PostgresTenantIsolationTests(SharedPostgresContainer postgres) : PostgresTestBase(postgres)
{
    [Fact]
    public async Task GlobalQueryFilters_IsolateProductsAndReviewsAcrossTenants()
    {
        var ct = TestContext.Current.CancellationToken;
        var actorId = Guid.NewGuid();
        var tenantA = new Tenant { Id = Guid.NewGuid(), Code = $"tenant-a-{Guid.NewGuid():N}", Name = "Tenant A" };
        var tenantB = new Tenant { Id = Guid.NewGuid(), Code = $"tenant-b-{Guid.NewGuid():N}", Name = "Tenant B" };
        var categoryA = new Category { Id = Guid.NewGuid(), TenantId = tenantA.Id, Name = $"Category-A-{Guid.NewGuid():N}" };
        var categoryB = new Category { Id = Guid.NewGuid(), TenantId = tenantB.Id, Name = $"Category-B-{Guid.NewGuid():N}" };
        var userA = new AppUser { Id = Guid.NewGuid(), TenantId = tenantA.Id, Username = $"usera-{Guid.NewGuid():N}", Email = $"a-{Guid.NewGuid():N}@example.com" };
        var userB = new AppUser { Id = Guid.NewGuid(), TenantId = tenantB.Id, Username = $"userb-{Guid.NewGuid():N}", Email = $"b-{Guid.NewGuid():N}@example.com" };
        var productA = new Product { Id = Guid.NewGuid(), TenantId = tenantA.Id, Name = $"Product-A-{Guid.NewGuid():N}", Price = 10m, CategoryId = categoryA.Id };
        var productB = new Product { Id = Guid.NewGuid(), TenantId = tenantB.Id, Name = $"Product-B-{Guid.NewGuid():N}", Price = 20m, CategoryId = categoryB.Id };
        var reviewA = new ProductReview { Id = Guid.NewGuid(), TenantId = tenantA.Id, ProductId = productA.Id, UserId = userA.Id, Rating = 5 };
        var reviewB = new ProductReview { Id = Guid.NewGuid(), TenantId = tenantB.Id, ProductId = productB.Id, UserId = userB.Id, Rating = 4 };

        await using (var seedContext = await CreateDbContextAsync(hasTenant: false, Guid.Empty, actorId, ct))
        {
            seedContext.Tenants.AddRange(tenantA, tenantB);
            seedContext.Users.AddRange(userA, userB);
            seedContext.Categories.AddRange(categoryA, categoryB);
            seedContext.Products.AddRange(productA, productB);
            seedContext.ProductReviews.AddRange(reviewA, reviewB);
            await seedContext.SaveChangesAsync(ct);
        }

        await using var tenantAContext = await CreateDbContextAsync(true, tenantA.Id, actorId, ct);
        await using var tenantBContext = await CreateDbContextAsync(true, tenantB.Id, actorId, ct);
        await using var unrestrictedContext = await CreateDbContextAsync(false, Guid.Empty, actorId, ct);

        var tenantAProducts = await tenantAContext.Products.OrderBy(p => p.Id).ToListAsync(ct);
        var tenantAReviews = await tenantAContext.ProductReviews.OrderBy(r => r.Id).ToListAsync(ct);
        var tenantBProducts = await tenantBContext.Products.OrderBy(p => p.Id).ToListAsync(ct);
        var tenantBReviews = await tenantBContext.ProductReviews.OrderBy(r => r.Id).ToListAsync(ct);
        var allProducts = await unrestrictedContext.Products
            .IgnoreQueryFilters()
            .Where(p => p.Id == productA.Id || p.Id == productB.Id)
            .OrderBy(p => p.Id)
            .ToListAsync(ct);
        var allReviews = await unrestrictedContext.ProductReviews
            .IgnoreQueryFilters()
            .Where(r => r.Id == reviewA.Id || r.Id == reviewB.Id)
            .OrderBy(r => r.Id)
            .ToListAsync(ct);

        tenantAProducts.Select(p => p.Id).ShouldBe([productA.Id]);
        tenantAReviews.Select(r => r.Id).ShouldBe([reviewA.Id]);
        tenantBProducts.Select(p => p.Id).ShouldBe([productB.Id]);
        tenantBReviews.Select(r => r.Id).ShouldBe([reviewB.Id]);
        allProducts.Select(p => p.Id).ShouldBe([productA.Id, productB.Id], ignoreOrder: true);
        allReviews.Select(r => r.Id).ShouldBe([reviewA.Id, reviewB.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task CategoryStats_ReturnsTenantScopedAndSoftDeleteScopedValues()
    {
        var ct = TestContext.Current.CancellationToken;
        var usernameA = $"tenant-a-{Guid.NewGuid():N}";
        var usernameB = $"tenant-b-{Guid.NewGuid():N}";

        var (tenantA, userA) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            usernameA,
            $"{usernameA}@example.com",
            ct: ct);

        var (tenantB, userB) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            usernameB,
            $"{usernameB}@example.com",
            ct: ct);

        Guid categoryAId;
        Guid categoryBId;
        Guid productAId;
        Guid reviewToSoftDeleteId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var categoryA = new Category
            {
                Id = Guid.NewGuid(),
                TenantId = tenantA.Id,
                Name = $"Category-A-{Guid.NewGuid():N}"
            };

            var categoryB = new Category
            {
                Id = Guid.NewGuid(),
                TenantId = tenantB.Id,
                Name = $"Category-B-{Guid.NewGuid():N}"
            };

            var productA1 = new Product
            {
                Id = Guid.NewGuid(),
                TenantId = tenantA.Id,
                Name = $"Product-A1-{Guid.NewGuid():N}",
                Price = 100m,
                CategoryId = categoryA.Id
            };

            var productA2 = new Product
            {
                Id = Guid.NewGuid(),
                TenantId = tenantA.Id,
                Name = $"Product-A2-{Guid.NewGuid():N}",
                Price = 300m,
                CategoryId = categoryA.Id
            };

            var productB1 = new Product
            {
                Id = Guid.NewGuid(),
                TenantId = tenantB.Id,
                Name = $"Product-B1-{Guid.NewGuid():N}",
                Price = 999m,
                CategoryId = categoryB.Id
            };

            var reviewA1 = new ProductReview
            {
                Id = Guid.NewGuid(),
                TenantId = tenantA.Id,
                ProductId = productA1.Id,
                UserId = userA.Id,
                Rating = 5
            };

            var reviewA2 = new ProductReview
            {
                Id = Guid.NewGuid(),
                TenantId = tenantA.Id,
                ProductId = productA1.Id,
                UserId = userA.Id,
                Rating = 4
            };

            var reviewB1 = new ProductReview
            {
                Id = Guid.NewGuid(),
                TenantId = tenantB.Id,
                ProductId = productB1.Id,
                UserId = userB.Id,
                Rating = 3
            };

            db.Categories.AddRange(categoryA, categoryB);
            db.Products.AddRange(productA1, productA2, productB1);
            db.ProductReviews.AddRange(reviewA1, reviewA2, reviewB1);
            await db.SaveChangesAsync(ct);

            // Mark one review and one product as soft-deleted to verify SQL function filtering.
            reviewA2.IsDeleted = true;
            reviewA2.DeletedAtUtc = DateTime.UtcNow;
            reviewA2.DeletedBy = Guid.NewGuid();

            productA2.IsDeleted = true;
            productA2.DeletedAtUtc = DateTime.UtcNow;
            productA2.DeletedBy = Guid.NewGuid();
            await db.SaveChangesAsync(ct);

            categoryAId = categoryA.Id;
            categoryBId = categoryB.Id;
            productAId = productA1.Id;
            reviewToSoftDeleteId = reviewA2.Id;
        }

        IntegrationAuthHelper.Authenticate(_client, tenantId: tenantA.Id, username: usernameA, role: Domain.Enums.UserRole.User);

        var statsResponse = await _client.GetAsync($"/api/v1/categories/{categoryAId}/stats", ct);
        statsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = await statsResponse.Content.ReadFromJsonAsync<ProductCategoryStatsResponse>(TestJsonOptions.CaseInsensitive, ct);
        payload.ShouldNotBeNull();
        payload!.CategoryId.ShouldBe(categoryAId);
        payload.ProductCount.ShouldBe(1);
        payload.AveragePrice.ShouldBe(100m);
        payload.TotalReviews.ShouldBe(1);

        // Tenant A token must not access stats of tenant B category.
        var forbiddenByIsolation = await _client.GetAsync($"/api/v1/categories/{categoryBId}/stats", ct);
        forbiddenByIsolation.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Ensure soft-deleted review is hidden from tenant-scoped query path.
        var reviewById = await _client.GetAsync($"/api/v1/productreviews/{reviewToSoftDeleteId}", ct);
        reviewById.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var reviewsByProduct = await _client.GetAsync($"/api/v1/productreviews/by-product/{productAId}", ct);
        reviewsByProduct.StatusCode.ShouldBe(HttpStatusCode.OK);
        var reviews = await reviewsByProduct.Content.ReadFromJsonAsync<ProductReviewResponse[]>(TestJsonOptions.CaseInsensitive, ct);
        reviews.ShouldNotBeNull();
        reviews!.Length.ShouldBe(1);
    }

    [Fact]
    public async Task CategoryStats_FunctionCallable_ReturnsZeroValuesForEmptyCategory()
    {
        var ct = TestContext.Current.CancellationToken;
        var username = $"stats-smoke-{Guid.NewGuid():N}";
        var (tenant, _) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            username,
            $"{username}@example.com",
            ct: ct);

        Guid categoryId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var category = new Category
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Name = $"Category-Stats-Smoke-{Guid.NewGuid():N}"
            };

            db.Categories.Add(category);
            await db.SaveChangesAsync(ct);
            categoryId = category.Id;
        }

        IntegrationAuthHelper.Authenticate(_client, tenantId: tenant.Id, username: username, role: Domain.Enums.UserRole.User);

        var response = await _client.GetAsync($"/api/v1/categories/{categoryId}/stats", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ProductCategoryStatsResponse>(TestJsonOptions.CaseInsensitive, ct);
        payload.ShouldNotBeNull();
        payload!.CategoryId.ShouldBe(categoryId);
        payload.ProductCount.ShouldBe(0);
        payload.AveragePrice.ShouldBe(0m);
        payload.TotalReviews.ShouldBe(0);
    }
}
