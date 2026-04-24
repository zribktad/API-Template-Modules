using Microsoft.EntityFrameworkCore;
using ProductCatalog.Entities;
using ProductCatalog.Persistence;
using ProductCatalog.Persistence.Interceptors;
using ProductCatalog.ValueObjects;
using SharedKernel.Application.Context;
using SharedKernel.Infrastructure.Auditing;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Postgres;

/// <summary>
///     Verifies that <see cref="ProductLinkSoftDeleteCascadeInterceptor" /> automatically cascades
///     a tracked <see cref="Product" /> soft-delete to its <see cref="ProductDataLink" /> rows.
/// </summary>
[Trait("Category", "Integration.Docker")]
public sealed class ProductLinkSoftDeleteCascadeInterceptorTests
    : IClassFixture<SharedPostgresContainer>,
        IAsyncLifetime
{
    private readonly SharedPostgresContainer _postgres;
    private string _connectionString = null!;
    private Guid _tenantId;
    private Guid _actorId;

    public ProductLinkSoftDeleteCascadeInterceptorTests(SharedPostgresContainer postgres)
    {
        _postgres = postgres;
    }

    public async ValueTask InitializeAsync()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string databaseName = $"interceptor_{Guid.NewGuid():N}";
        _connectionString = await IsolatedPostgresDatabase.CreateAndGetConnectionStringAsync(
            _postgres,
            databaseName,
            ct
        );
        _tenantId = Guid.NewGuid();
        _actorId = Guid.NewGuid();

        await using ProductCatalogDbContext migrateCtx = BuildContext(_tenantId, _actorId);
        await migrateCtx.Database.MigrateAsync(ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task SoftDeletingTrackedProduct_CascadesToLinks()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid categoryId = Guid.NewGuid();
        Guid productId = Guid.NewGuid();
        Guid link1Id = Guid.NewGuid();
        Guid link2Id = Guid.NewGuid();

        await using (ProductCatalogDbContext seedCtx = BuildContextNoInterceptor(_tenantId, _actorId))
        {
            seedCtx.Categories.Add(new Category { Id = categoryId, TenantId = _tenantId, Name = "Cat" });
            seedCtx.Products.Add(new Product
            {
                Id = productId,
                TenantId = _tenantId,
                Name = "Product A",
                Price = Price.Zero,
                CategoryId = categoryId,
            });
            seedCtx.ProductDataLinks.Add(new ProductDataLink { ProductId = productId, ProductDataId = link1Id, TenantId = _tenantId });
            seedCtx.ProductDataLinks.Add(new ProductDataLink { ProductId = productId, ProductDataId = link2Id, TenantId = _tenantId });
            await seedCtx.SaveChangesAsync(ct);
        }

        await using (ProductCatalogDbContext deleteCtx = BuildContext(_tenantId, _actorId))
        {
            Product product = await deleteCtx.Products.SingleAsync(p => p.Id == productId, ct);
            deleteCtx.Products.Remove(product);
            await deleteCtx.SaveChangesAsync(ct);
        }

        await using ProductCatalogDbContext verifyCtx = BuildContextNoInterceptor(_tenantId, _actorId);
        List<ProductDataLink> links = await verifyCtx
            .ProductDataLinks.IgnoreQueryFilters()
            .Where(l => l.ProductId == productId)
            .ToListAsync(ct);

        links.Count.ShouldBe(2);
        links.ShouldAllBe(l => l.IsDeleted);
        links.ShouldAllBe(l => l.DeletedAtUtc.HasValue);
        links.ShouldAllBe(l => l.DeletedBy == _actorId);
    }

    [Fact]
    public async Task SoftDeletingTrackedProduct_OnlyCascadesToActiveLinks()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid categoryId = Guid.NewGuid();
        Guid productId = Guid.NewGuid();
        Guid activeLinkDataId = Guid.NewGuid();
        Guid alreadyDeletedLinkDataId = Guid.NewGuid();
        DateTime preExistingDeletedAt = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await using (ProductCatalogDbContext seedCtx = BuildContextNoInterceptor(_tenantId, _actorId))
        {
            seedCtx.Categories.Add(new Category { Id = categoryId, TenantId = _tenantId, Name = "Cat2" });
            seedCtx.Products.Add(new Product
            {
                Id = productId,
                TenantId = _tenantId,
                Name = "Product B",
                Price = Price.Zero,
                CategoryId = categoryId,
            });
            seedCtx.ProductDataLinks.Add(new ProductDataLink { ProductId = productId, ProductDataId = activeLinkDataId, TenantId = _tenantId });
            ProductDataLink alreadyDeleted = new()
            {
                ProductId = productId,
                ProductDataId = alreadyDeletedLinkDataId,
                TenantId = _tenantId,
                IsDeleted = true,
                DeletedAtUtc = preExistingDeletedAt,
                DeletedBy = Guid.NewGuid(),
            };
            seedCtx.ProductDataLinks.Add(alreadyDeleted);
            await seedCtx.SaveChangesAsync(ct);
        }

        await using (ProductCatalogDbContext deleteCtx = BuildContext(_tenantId, _actorId))
        {
            Product product = await deleteCtx.Products.SingleAsync(p => p.Id == productId, ct);
            deleteCtx.Products.Remove(product);
            await deleteCtx.SaveChangesAsync(ct);
        }

        await using ProductCatalogDbContext verifyCtx = BuildContextNoInterceptor(_tenantId, _actorId);
        List<ProductDataLink> links = await verifyCtx
            .ProductDataLinks.IgnoreQueryFilters()
            .Where(l => l.ProductId == productId)
            .OrderBy(l => l.ProductDataId)
            .ToListAsync(ct);

        ProductDataLink activeLink = links.Single(l => l.ProductDataId == activeLinkDataId);
        ProductDataLink oldLink = links.Single(l => l.ProductDataId == alreadyDeletedLinkDataId);

        activeLink.IsDeleted.ShouldBeTrue();
        activeLink.DeletedBy.ShouldBe(_actorId);

        // pre-existing deleted link must not have its audit stamps overwritten
        oldLink.DeletedAtUtc.ShouldBe(preExistingDeletedAt);
    }

    [Fact]
    public async Task ModifyingProductWithoutSoftDelete_DoesNotTouchLinks()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid categoryId = Guid.NewGuid();
        Guid productId = Guid.NewGuid();
        Guid linkDataId = Guid.NewGuid();

        await using (ProductCatalogDbContext seedCtx = BuildContextNoInterceptor(_tenantId, _actorId))
        {
            seedCtx.Categories.Add(new Category { Id = categoryId, TenantId = _tenantId, Name = "Cat3" });
            seedCtx.Products.Add(new Product
            {
                Id = productId,
                TenantId = _tenantId,
                Name = "Product C",
                Price = Price.Zero,
                CategoryId = categoryId,
            });
            seedCtx.ProductDataLinks.Add(new ProductDataLink { ProductId = productId, ProductDataId = linkDataId, TenantId = _tenantId });
            await seedCtx.SaveChangesAsync(ct);
        }

        await using (ProductCatalogDbContext updateCtx = BuildContext(_tenantId, _actorId))
        {
            Product product = await updateCtx.Products.SingleAsync(p => p.Id == productId, ct);
            product.Name = "Product C Updated";
            await updateCtx.SaveChangesAsync(ct);
        }

        await using ProductCatalogDbContext verifyCtx = BuildContextNoInterceptor(_tenantId, _actorId);
        ProductDataLink link = await verifyCtx
            .ProductDataLinks.IgnoreQueryFilters()
            .SingleAsync(l => l.ProductDataId == linkDataId, ct);

        link.IsDeleted.ShouldBeFalse();
        link.DeletedAtUtc.ShouldBeNull();
    }

    private ProductCatalogDbContext BuildContext(Guid tenantId, Guid actorId)
    {
        TestActorProvider actor = new(actorId);
        TestTenantProvider tenant = new(tenantId);
        ProductLinkSoftDeleteCascadeInterceptor interceptor = new(actor, TimeProvider.System);

        DbContextOptions<ProductCatalogDbContext> options = new DbContextOptionsBuilder<ProductCatalogDbContext>()
            .UseNpgsql(_connectionString)
            .AddInterceptors(interceptor)
            .Options;

        return new ProductCatalogDbContext(options, tenant, actor, TimeProvider.System, new AuditableEntityStateManager());
    }

    private ProductCatalogDbContext BuildContextNoInterceptor(Guid tenantId, Guid actorId)
    {
        TestActorProvider actor = new(actorId);
        TestTenantProvider tenant = new(tenantId);

        DbContextOptions<ProductCatalogDbContext> options = new DbContextOptionsBuilder<ProductCatalogDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        return new ProductCatalogDbContext(options, tenant, actor, TimeProvider.System, new AuditableEntityStateManager());
    }

    private sealed class TestActorProvider(Guid actorId) : IActorProvider
    {
        public Guid ActorId => actorId;
    }

    private sealed class TestTenantProvider(Guid tenantId) : ITenantProvider
    {
        public Guid TenantId => tenantId;
        public bool HasTenant => true;
    }
}
