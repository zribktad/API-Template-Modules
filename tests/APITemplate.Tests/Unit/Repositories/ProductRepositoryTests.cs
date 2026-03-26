using APITemplate.Domain.Entities;
using APITemplate.Domain.Exceptions;
using APITemplate.Application.Common.Context;
using APITemplate.Infrastructure.Persistence;
using APITemplate.Infrastructure.Persistence.Auditing;
using APITemplate.Infrastructure.Persistence.EntityNormalization;
using APITemplate.Infrastructure.Persistence.SoftDelete;
using APITemplate.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Repositories;

public class ProductRepositoryTests : IDisposable
{
    private static readonly Guid TestTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly AppDbContext _dbContext;
    private readonly ProductRepository _sut;

    public ProductRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = CreateDbContext(options);
        _sut = new ProductRepository(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task AddAsync_PersistsProduct()
    {
        var ct = TestContext.Current.CancellationToken;
        var product = CreateProduct("Test Product", 10m);

        var result = await _sut.AddAsync(product, ct);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(product.Id);

        var persisted = await _dbContext.Products.FindAsync([product.Id], ct);
        persisted.ShouldNotBeNull();
        persisted!.Name.ShouldBe("Test Product");
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_ReturnsProduct()
    {
        var ct = TestContext.Current.CancellationToken;
        var product = CreateProduct("Existing", 5m);
        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync(ct);

        var result = await _sut.GetByIdAsync(product.Id, ct);

        result.ShouldNotBeNull();
        result!.Name.ShouldBe("Existing");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_ModifiesProduct()
    {
        var ct = TestContext.Current.CancellationToken;
        var product = CreateProduct("Original", 10m);
        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync(ct);
        _dbContext.Entry(product).State = EntityState.Detached;

        product.Name = "Updated";
        product.Price = 25m;
        await _sut.UpdateAsync(product, ct);

        var updated = await _dbContext.Products.FindAsync([product.Id], ct);
        updated!.Name.ShouldBe("Updated");
        updated.Price.ShouldBe(25m);
    }

    [Fact]
    public async Task DeleteAsync_WhenExists_RemovesProduct()
    {
        var ct = TestContext.Current.CancellationToken;
        var product = CreateProduct("ToDelete", 10m);
        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync(ct);

        await _sut.DeleteAsync(product.Id, ct);
        await _dbContext.SaveChangesAsync(ct);

        var deleted = await _dbContext.Products.FindAsync([product.Id], ct);
        deleted.ShouldNotBeNull();
        deleted!.IsDeleted.ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteAsync_WhenNotExists_ThrowsNotFoundException()
    {
        var act = () => _sut.DeleteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        await Should.ThrowAsync<NotFoundException>(act);
    }

    private static Product CreateProduct(string name, decimal price)
    {
        return new Product
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantId,
            Name = name,
            Price = price,
            Audit = new() { CreatedAtUtc = DateTime.UtcNow }
        };
    }

    private static AppDbContext CreateDbContext(DbContextOptions<AppDbContext> options)
    {
        var stateManager = new AuditableEntityStateManager();

        return new AppDbContext(
            options,
            new TestTenantProvider(),
            new TestActorProvider(),
            TimeProvider.System,
            [],
            new AppUserEntityNormalizationService(),
            stateManager,
            new SoftDeleteProcessor(stateManager));
    }

    private sealed class TestTenantProvider : ITenantProvider
    {
        public Guid TenantId => TestTenantId;
        public bool HasTenant => true;
    }

    private sealed class TestActorProvider : IActorProvider
    {
        public Guid ActorId => Guid.Empty;
    }
}
