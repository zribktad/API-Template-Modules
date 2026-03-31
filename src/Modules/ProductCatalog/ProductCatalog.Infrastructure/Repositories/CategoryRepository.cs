using SharedKernel.Application.Context;
using ProductCatalog.Domain.Entities;
using ProductCatalog.Domain.Interfaces;
using ProductCatalog.Infrastructure.Persistence;
using ProductCatalog.Infrastructure.StoredProcedures;

namespace ProductCatalog.Infrastructure.Repositories;

/// <summary>
/// EF Core repository for <see cref="Category"/> that extends the base repository with
/// stored-procedure-based stats retrieval.
/// </summary>
public sealed class CategoryRepository : RepositoryBase<Category>, ICategoryRepository
{
    private readonly IStoredProcedureExecutor _spExecutor;
    private readonly ITenantProvider _tenantProvider;

    public CategoryRepository(
        ProductCatalogDbContext dbContext,
        IStoredProcedureExecutor spExecutor,
        ITenantProvider tenantProvider
    )
        : base(dbContext)
    {
        _spExecutor = spExecutor;
        _tenantProvider = tenantProvider;
    }

    /// <summary>
    /// Retrieves aggregate product statistics for the given category via a stored procedure,
    /// passing the current tenant ID explicitly to enforce data isolation at the DB level.
    /// </summary>
    public Task<ProductCategoryStats?> GetStatsByIdAsync(
        Guid categoryId,
        CancellationToken ct = default
    )
    {
        // Stored procedures bypass EF global query filters, so tenant must be passed explicitly for DB-side isolation.
        return _spExecutor.QueryFirstAsync(
            new GetProductCategoryStatsProcedure(categoryId, _tenantProvider.TenantId),
            ct
        );
    }
}


