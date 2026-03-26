using APITemplate.Application.Common.Context;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace APITemplate.Infrastructure.Repositories;

/// <summary>
/// EF Core repository for <see cref="ProductDataLink"/> join entities, providing queries
/// that selectively bypass global filters when deleted links must be included.
/// </summary>
public sealed class ProductDataLinkRepository : IProductDataLinkRepository
{
    private readonly AppDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;

    public ProductDataLinkRepository(AppDbContext dbContext, ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
    }

    /// <summary>
    /// Returns links for the given product, optionally including soft-deleted entries by bypassing global filters.
    /// </summary>
    public async Task<IReadOnlyList<ProductDataLink>> ListByProductIdAsync(
        Guid productId,
        bool includeDeleted = false,
        CancellationToken ct = default
    )
    {
        var query = includeDeleted
            ? _dbContext
                .ProductDataLinks.IgnoreQueryFilters()
                .Where(link =>
                    link.TenantId == _tenantProvider.TenantId && link.ProductId == productId
                )
            : _dbContext.ProductDataLinks.Where(link => link.ProductId == productId);

        return await query.ToListAsync(ct);
    }

    public async Task<
        IReadOnlyDictionary<Guid, IReadOnlyList<ProductDataLink>>
    > ListByProductIdsAsync(
        IReadOnlyCollection<Guid> productIds,
        bool includeDeleted = false,
        CancellationToken ct = default
    )
    {
        if (productIds.Count == 0)
            return new Dictionary<Guid, IReadOnlyList<ProductDataLink>>();

        var query = includeDeleted
            ? _dbContext
                .ProductDataLinks.IgnoreQueryFilters()
                .Where(link =>
                    link.TenantId == _tenantProvider.TenantId && productIds.Contains(link.ProductId)
                )
            : _dbContext.ProductDataLinks.Where(link => productIds.Contains(link.ProductId));

        var links = await query.ToListAsync(ct);
        return links
            .GroupBy(link => link.ProductId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ProductDataLink>)group.ToList()
            );
    }

    /// <summary>Returns <c>true</c> when at least one active link references the specified product data document.</summary>
    public Task<bool> HasActiveLinksForProductDataAsync(
        Guid productDataId,
        CancellationToken ct = default
    ) => _dbContext.ProductDataLinks.AnyAsync(link => link.ProductDataId == productDataId, ct);

    /// <summary>
    /// Stages removal of all active links for the given product data document so they
    /// are soft-deleted when the unit of work commits.
    /// </summary>
    public async Task SoftDeleteActiveLinksForProductDataAsync(
        Guid productDataId,
        CancellationToken ct = default
    )
    {
        var links = await _dbContext
            .ProductDataLinks.Where(link => link.ProductDataId == productDataId)
            .ToListAsync(ct);

        if (links.Count == 0)
            return;

        _dbContext.ProductDataLinks.RemoveRange(links);
    }
}
