using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using APITemplate.Application.Common.DTOs;
using APITemplate.Application.Common.Events;
using APITemplate.Application.Features.Product;
using APITemplate.Application.Features.Product.Repositories;
using APITemplate.Application.Features.ProductReview;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Persistence;
using APITemplate.Infrastructure.Repositories;
using APITemplate.Tests.Integration.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Wolverine;
using Xunit;

namespace APITemplate.Tests.Integration.Postgres;

public sealed class PostgresSoftDeleteTests(SharedPostgresContainer postgres)
    : PostgresTestBase(postgres)
{
    [Fact]
    public async Task DeleteProduct_WithExistingReviews_SoftDeletesReviews_Cascade()
    {
        var ct = TestContext.Current.CancellationToken;
        var username = $"cascade-{Guid.NewGuid():N}";
        var (tenant, seededUser) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            username,
            $"{username}@example.com",
            ct: ct
        );

        Guid productId;
        Guid review1Id;
        Guid review2Id;
        Guid categoryId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var category = new Category
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Name = $"Category-Cascade-{Guid.NewGuid():N}",
            };

            db.Categories.Add(category);
            await db.SaveChangesAsync(ct);
            categoryId = category.Id;
        }

        IntegrationAuthHelper.Authenticate(
            _client,
            userId: seededUser.Id,
            tenantId: tenant.Id,
            username: username,
            role: Domain.Enums.UserRole.TenantAdmin
        );

        var productName = $"Product-Cascade-{Guid.NewGuid():N}";
        var createProductResponse = await _client.PostAsJsonAsync(
            "/api/v1/products",
            new
            {
                Items = new[]
                {
                    new
                    {
                        Name = productName,
                        Price = 88m,
                        CategoryId = categoryId,
                    },
                },
            },
            ct
        );
        var createProductBody = await createProductResponse.Content.ReadAsStringAsync(ct);
        createProductResponse.StatusCode.ShouldBe(HttpStatusCode.OK, createProductBody);

        var createProductBatch = JsonSerializer.Deserialize<BatchResponse>(
            createProductBody,
            TestJsonOptions.CaseInsensitive
        );
        createProductBatch.ShouldNotBeNull();
        createProductBatch!.Failures.ShouldBeEmpty();
        productId = await ResolveProductIdAsync(productName, 88m, categoryId, ct);

        var createReview1 = await _client.PostAsJsonAsync(
            "/api/v1/productreviews",
            new { ProductId = productId, Rating = 5 },
            ct
        );
        createReview1.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createdReview1 = await createReview1.Content.ReadFromJsonAsync<ProductReviewResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        createdReview1.ShouldNotBeNull();
        review1Id = createdReview1!.Id;

        var createReview2 = await _client.PostAsJsonAsync(
            "/api/v1/productreviews",
            new { ProductId = productId, Rating = 4 },
            ct
        );
        createReview2.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createdReview2 = await createReview2.Content.ReadFromJsonAsync<ProductReviewResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        createdReview2.ShouldNotBeNull();
        review2Id = createdReview2!.Id;

        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/products")
        {
            Content = JsonContent.Create(new { Ids = new[] { productId } }),
        };
        var deleteResponse = await _client.SendAsync(deleteRequest, ct);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var reviewsResponse = await _client.GetAsync(
            $"/api/v1/productreviews/by-product/{productId}",
            ct
        );
        reviewsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var visibleReviews =
            await reviewsResponse.Content.ReadFromJsonAsync<ProductReviewResponse[]>(
                TestJsonOptions.CaseInsensitive,
                ct
            );
        visibleReviews.ShouldNotBeNull();
        visibleReviews.ShouldBeEmpty();

        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var allReviews = await verifyDb
            .ProductReviews.IgnoreQueryFilters()
            .Where(r => r.Id == review1Id || r.Id == review2Id)
            .ToListAsync(ct);

        allReviews.Count.ShouldBe(2);
        allReviews.All(r => r.IsDeleted).ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteProduct_SoftDeletePipeline_SetsAuditFieldsAndKeepsDependentsPhysicallyPresent()
    {
        var ct = TestContext.Current.CancellationToken;
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Code = $"tenant-soft-{Guid.NewGuid():N}",
            Name = "Tenant Soft Delete",
        };
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Username = $"user-soft-{Guid.NewGuid():N}",
            Email = $"soft-{Guid.NewGuid():N}@example.com",
        };
        var category = new Category
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = $"Category-Soft-{Guid.NewGuid():N}",
        };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = $"Product-Soft-{Guid.NewGuid():N}",
            Price = 50m,
            CategoryId = category.Id,
        };
        var review1 = new ProductReview
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = product.Id,
            UserId = user.Id,
            Rating = 5,
        };
        var review2 = new ProductReview
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = product.Id,
            UserId = user.Id,
            Rating = 3,
        };

        await using (
            var seedContext = await CreateDbContextAsync(hasTenant: false, Guid.Empty, actorId, ct)
        )
        {
            seedContext.Tenants.Add(tenant);
            seedContext.Users.Add(user);
            seedContext.Categories.Add(category);
            seedContext.Products.Add(product);
            seedContext.ProductReviews.AddRange(review1, review2);
            await seedContext.SaveChangesAsync(ct);
        }

        await using (var deleteContext = await CreateDbContextAsync(true, tenantId, actorId, ct))
        {
            var repository = new ProductRepository(deleteContext);
            var unitOfWork = CreateUnitOfWork(deleteContext);

            await repository.DeleteAsync(product.Id, ct);
            await unitOfWork.CommitAsync(ct);
        }

        await using var tenantContext = await CreateDbContextAsync(true, tenantId, actorId, ct);
        await using var unrestrictedContext = await CreateDbContextAsync(
            false,
            Guid.Empty,
            actorId,
            ct
        );

        var visibleReviews = await tenantContext
            .ProductReviews.Where(r => r.ProductId == product.Id)
            .ToListAsync(ct);
        visibleReviews.ShouldBeEmpty();

        var deletedProduct = await unrestrictedContext
            .Products.IgnoreQueryFilters()
            .SingleAsync(p => p.Id == product.Id, ct);
        var deletedReviews = await unrestrictedContext
            .ProductReviews.IgnoreQueryFilters()
            .Where(r => r.ProductId == product.Id)
            .OrderBy(r => r.Id)
            .ToListAsync(ct);

        deletedProduct.IsDeleted.ShouldBeTrue();
        deletedProduct.DeletedAtUtc.ShouldNotBeNull();
        deletedProduct.DeletedBy.ShouldBe(actorId);
        deletedReviews.Count.ShouldBe(2);
        deletedReviews.All(r => r.IsDeleted).ShouldBeTrue();
        deletedReviews.All(r => r.DeletedAtUtc.HasValue).ShouldBeTrue();
        deletedReviews.All(r => r.DeletedBy == actorId).ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteProduct_SoftDeletesProductDataLinks()
    {
        var ct = TestContext.Current.CancellationToken;
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Code = $"tenant-links-{Guid.NewGuid():N}",
            Name = "Tenant Links",
        };
        var category = new Category
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = $"Category-Links-{Guid.NewGuid():N}",
        };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = $"Product-Links-{Guid.NewGuid():N}",
            Price = 50m,
            CategoryId = category.Id,
        };
        var productDataId = Guid.NewGuid();

        await using (
            var seedContext = await CreateDbContextAsync(hasTenant: false, Guid.Empty, actorId, ct)
        )
        {
            seedContext.Tenants.Add(tenant);
            seedContext.Categories.Add(category);
            seedContext.Products.Add(product);
            seedContext.ProductDataLinks.Add(
                new ProductDataLink
                {
                    ProductId = product.Id,
                    ProductDataId = productDataId,
                    TenantId = tenantId,
                }
            );
            await seedContext.SaveChangesAsync(ct);
        }

        await using (var deleteContext = await CreateDbContextAsync(true, tenantId, actorId, ct))
        {
            await DeleteProductsCommandHandler.HandleAsync(
                new DeleteProductsCommand(new BatchDeleteRequest([product.Id])),
                new ProductRepository(deleteContext),
                CreateUnitOfWork(deleteContext),
                Mock.Of<IMessageBus>(),
                ct
            );
        }

        await using var verifyContext = await CreateDbContextAsync(false, Guid.Empty, actorId, ct);
        var remainingLinks = await verifyContext
            .ProductDataLinks.IgnoreQueryFilters()
            .Where(link => link.ProductId == product.Id)
            .ToListAsync(ct);

        remainingLinks.Count.ShouldBe(1);
        remainingLinks[0].IsDeleted.ShouldBeTrue();
        remainingLinks[0].DeletedAtUtc.ShouldNotBeNull();
        remainingLinks[0].DeletedBy.ShouldBe(actorId);
    }

    [Fact]
    public async Task DeleteProductData_SoftDeletesLinksAndMongoDocument()
    {
        var ct = TestContext.Current.CancellationToken;
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var productDataId = Guid.NewGuid();
        var mongoRepositoryMock = _factory.Services.GetRequiredService<
            Mock<IProductDataRepository>
        >();
        mongoRepositoryMock.Reset();
        mongoRepositoryMock
            .Setup(r => r.GetByIdAsync(productDataId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ImageProductData
                {
                    Id = productDataId,
                    TenantId = tenantId,
                    Title = "Image",
                }
            );

        var tenant = new Tenant
        {
            Id = tenantId,
            Code = $"tenant-pd-{Guid.NewGuid():N}",
            Name = "Tenant ProductData",
        };
        var category = new Category
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = $"Category-PD-{Guid.NewGuid():N}",
        };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = $"Product-PD-{Guid.NewGuid():N}",
            Price = 50m,
            CategoryId = category.Id,
        };

        await using (var seedContext = await CreateDbContextAsync(false, Guid.Empty, actorId, ct))
        {
            seedContext.Tenants.Add(tenant);
            seedContext.Categories.Add(category);
            seedContext.Products.Add(product);
            seedContext.ProductDataLinks.Add(
                new ProductDataLink
                {
                    ProductId = product.Id,
                    ProductDataId = productDataId,
                    TenantId = tenantId,
                }
            );
            await seedContext.SaveChangesAsync(ct);
        }

        IntegrationAuthHelper.Authenticate(_client, tenantId: tenantId);
        var response = await _client.DeleteAsync($"/api/v1/product-data/{productDataId}", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using var verifyContext = await CreateDbContextAsync(false, Guid.Empty, actorId, ct);
        var link = await verifyContext
            .ProductDataLinks.IgnoreQueryFilters()
            .SingleAsync(
                existing =>
                    existing.ProductId == product.Id && existing.ProductDataId == productDataId,
                ct
            );

        link.IsDeleted.ShouldBeTrue();
        mongoRepositoryMock.Verify(
            r =>
                r.SoftDeleteAsync(
                    productDataId,
                    It.IsAny<Guid>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    private async Task<Guid> ResolveProductIdAsync(
        string name,
        decimal price,
        Guid categoryId,
        CancellationToken ct
    )
    {
        var response = await _client.GetAsync(
            $"/api/v1/products?name={Uri.EscapeDataString(name)}&categoryIds={categoryId}",
            ct
        );
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ProductsResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        payload.ShouldNotBeNull();
        var item = payload!.Page.Items.FirstOrDefault(p =>
            p.Name == name && p.Price == price && p.CategoryId == categoryId
        );
        item.ShouldNotBeNull();
        return item!.Id;
    }
}
