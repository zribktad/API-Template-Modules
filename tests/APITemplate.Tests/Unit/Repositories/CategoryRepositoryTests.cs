using APITemplate.Domain.Entities;
using APITemplate.Domain.Exceptions;
using APITemplate.Domain.Interfaces;
using APITemplate.Application.Common.Context;
using APITemplate.Infrastructure.Persistence;
using APITemplate.Infrastructure.Persistence.Auditing;
using APITemplate.Infrastructure.Persistence.EntityNormalization;
using APITemplate.Infrastructure.Persistence.SoftDelete;
using APITemplate.Infrastructure.Repositories;
using APITemplate.Infrastructure.StoredProcedures;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Repositories;

public class CategoryRepositoryTests : IDisposable
{
    private static readonly Guid TestTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly AppDbContext _dbContext;
    private readonly Mock<IStoredProcedureExecutor> _spExecutorMock;
    private readonly ITenantProvider _tenantProvider;
    private readonly CategoryRepository _sut;

    public CategoryRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _tenantProvider = new TestTenantProvider();
        _dbContext = CreateDbContext(options, _tenantProvider);
        _spExecutorMock = new Mock<IStoredProcedureExecutor>();
        _sut = new CategoryRepository(_dbContext, _spExecutorMock.Object, _tenantProvider);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task AddAsync_PersistsCategory()
    {
        var ct = TestContext.Current.CancellationToken;
        var category = CreateCategory("Electronics");

        var result = await _sut.AddAsync(category, ct);
        await _dbContext.SaveChangesAsync(ct);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(category.Id);

        var persisted = await _dbContext.Categories.FindAsync([category.Id], ct);
        persisted.ShouldNotBeNull();
        persisted!.Name.ShouldBe("Electronics");
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_ReturnsCategory()
    {
        var ct = TestContext.Current.CancellationToken;
        var category = CreateCategory("Books");
        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync(ct);

        var result = await _sut.GetByIdAsync(category.Id, ct);

        result.ShouldNotBeNull();
        result!.Name.ShouldBe("Books");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_ModifiesCategory()
    {
        var ct = TestContext.Current.CancellationToken;
        var category = CreateCategory("Old Name", "Old Description");
        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync(ct);
        _dbContext.Entry(category).State = EntityState.Detached;

        category.Name = "New Name";
        category.Description = "New Description";
        await _sut.UpdateAsync(category, ct);
        await _dbContext.SaveChangesAsync(ct);

        var updated = await _dbContext.Categories.FindAsync([category.Id], ct);
        updated!.Name.ShouldBe("New Name");
        updated.Description.ShouldBe("New Description");
    }

    [Fact]
    public async Task DeleteAsync_WhenExists_RemovesCategory()
    {
        var ct = TestContext.Current.CancellationToken;
        var category = CreateCategory("ToDelete");
        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync(ct);

        await _sut.DeleteAsync(category.Id, ct);
        await _dbContext.SaveChangesAsync(ct);

        var deleted = await _dbContext.Categories.FindAsync([category.Id], ct);
        deleted.ShouldNotBeNull();
        deleted!.IsDeleted.ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteAsync_WhenNotExists_ThrowsNotFoundException()
    {
        var act = () => _sut.DeleteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        await Should.ThrowAsync<NotFoundException>(act);
    }

    [Fact]
    public async Task GetStatsByIdAsync_WhenStatsExist_ReturnsStats()
    {
        var ct = TestContext.Current.CancellationToken;
        var categoryId = Guid.NewGuid();
        var expected = new ProductCategoryStats
        {
            CategoryId = categoryId,
            CategoryName = "Electronics",
            ProductCount = 3,
            AveragePrice = 150m,
            TotalReviews = 10
        };

        _spExecutorMock
            .Setup(e => e.QueryFirstAsync(
                It.IsAny<GetProductCategoryStatsProcedure>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.GetStatsByIdAsync(categoryId, ct);

        result.ShouldNotBeNull();
        result!.CategoryId.ShouldBe(categoryId);
        result.CategoryName.ShouldBe("Electronics");
        result.ProductCount.ShouldBe(3);
        result.AveragePrice.ShouldBe(150m);
        result.TotalReviews.ShouldBe(10);

        _spExecutorMock.Verify(e => e.QueryFirstAsync(
            It.IsAny<GetProductCategoryStatsProcedure>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetStatsByIdAsync_WhenCategoryNotFound_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        _spExecutorMock
            .Setup(e => e.QueryFirstAsync(
                It.IsAny<GetProductCategoryStatsProcedure>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductCategoryStats?)null);

        var result = await _sut.GetStatsByIdAsync(Guid.NewGuid(), ct);

        result.ShouldBeNull();
    }

    private static Category CreateCategory(string name, string? description = null)
    {
        return new Category
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantId,
            Name = name,
            Description = description,
            Audit = new() { CreatedAtUtc = DateTime.UtcNow }
        };
    }

    private static AppDbContext CreateDbContext(DbContextOptions<AppDbContext> options, ITenantProvider tenantProvider)
    {
        var stateManager = new AuditableEntityStateManager();

        return new AppDbContext(
            options,
            tenantProvider,
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
