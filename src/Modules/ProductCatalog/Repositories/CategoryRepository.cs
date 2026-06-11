using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProductCatalog.Persistence;
using ProductCatalog.StoredProcedures;

namespace ProductCatalog.Repositories;

/// <summary>
///     EF Core repository for <see cref="Category" /> that extends the base repository with
///     stored-procedure-based stats retrieval.
/// </summary>
public sealed class CategoryRepository : RepositoryBase<Category>, ICategoryRepository
{
    private readonly ProductCatalogDbContext _dbContext;
    private readonly IStoredProcedureExecutor<ProductCatalogDbMarker> _spExecutor;
    private readonly ITenantProvider _tenantProvider;

    public CategoryRepository(
        ProductCatalogDbContext dbContext,
        IStoredProcedureExecutor<ProductCatalogDbMarker> spExecutor,
        ITenantProvider tenantProvider
    )
        : base(dbContext)
    {
        _dbContext = dbContext;
        _spExecutor = spExecutor;
        _tenantProvider = tenantProvider;
    }

    /// <summary>
    ///     Retrieves aggregate product statistics for the given category via a stored procedure,
    ///     passing the current tenant ID explicitly to enforce data isolation at the DB level.
    /// </summary>
    public async Task<ProductCategoryStats?> GetStatsByIdAsync(
        Guid categoryId,
        CancellationToken ct = default
    )
    {
        try
        {
            // Stored procedures bypass EF global query filters, so tenant must be passed explicitly for DB-side isolation.
            return await _spExecutor.QueryFirstAsync(
                new GetProductCategoryStatsProcedure(categoryId, _tenantProvider.TenantId),
                ct
            );
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedFunction)
        {
            Category? category = await _dbContext
                .Categories.AsNoTracking()
                .FirstOrDefaultAsync(
                    c =>
                        c.Id == categoryId
                        && c.TenantId == _tenantProvider.TenantId
                        && !c.IsDeleted,
                    ct
                );

            return category is null
                ? null
                : new ProductCategoryStats
                {
                    CategoryId = category.Id,
                    CategoryName = category.Name,
                    ProductCount = 0,
                    AveragePrice = 0m,
                    TotalReviews = 0,
                };
        }
    }

    /// <inheritdoc />
    public async Task<int> BulkSoftDeleteByTenantAsync(
        Guid tenantId,
        Guid actorId,
        DateTime deletedAtUtc,
        CancellationToken ct = default
    )
    {
        return await _dbContext
            .Categories.IgnoreQueryFilters()
            .Where(category => category.TenantId == tenantId && !category.IsDeleted)
            .BulkSoftDeleteAsync(actorId, deletedAtUtc, ct);
    }

    /// <inheritdoc />
    public async Task<int> BulkSoftDeleteByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        Guid tenantId,
        Guid actorId,
        DateTime deletedAtUtc,
        CancellationToken ct = default
    )
    {
        return await _dbContext
            .Categories.IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && ids.Contains(c.Id) && !c.IsDeleted)
            .BulkSoftDeleteAsync(actorId, deletedAtUtc, ct);
    }
}
