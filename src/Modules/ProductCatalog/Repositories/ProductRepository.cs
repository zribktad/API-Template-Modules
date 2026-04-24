using ErrorOr;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Features.Product.GetProducts;
using ProductCatalog.Persistence;
using ProductApplicationRepository = ProductCatalog.Interfaces.IProductRepository;

namespace ProductCatalog.Repositories;

/// <summary>
///     EF Core repository for <see cref="Product" /> with specification-based listing,
///     count, category facet, and price bucket facet queries.
/// </summary>
public class ProductRepository : RepositoryBase<Product>, ProductApplicationRepository
{
    private static readonly IReadOnlyList<ProductPriceFacetBucketResponse> DefaultPriceBuckets =
    [
        new("0 - 50", 0m, 50m, 0),
        new("50 - 100", 50m, 100m, 0),
        new("100 - 250", 100m, 250m, 0),
        new("250 - 500", 250m, 500m, 0),
        new("500+", 500m, null, 0),
    ];

    private readonly ProductCatalogDbContext _dbContext;

    public ProductRepository(ProductCatalogDbContext dbContext)
        : base(dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>Returns a single-query paged result of products matching the given filter.</summary>
    public async Task<ErrorOr<PagedResponse<ProductResponse>>> GetPagedAsync(
        ProductFilter filter,
        CancellationToken ct = default
    )
    {
        return await GetPagedAsync(
            new ProductSpecification(filter),
            filter.PageNumber,
            filter.PageSize,
            ct
        );
    }

    /// <summary>
    ///     Returns category facet counts for products matching the filter, ordered by descending count then category
    ///     name.
    /// </summary>
    public async Task<IReadOnlyList<ProductCategoryFacetValue>> GetCategoryFacetsAsync(
        ProductFilter filter,
        CancellationToken ct = default
    )
    {
        ProductCategoryFacetSpecification specification = new(filter);
        IQueryable<Product> query =
            Ardalis.Specification.EntityFrameworkCore.SpecificationEvaluator.Default.GetQuery(
                _dbContext.Products.AsQueryable(),
                specification
            );

        return await query
            .GroupJoin(
                _dbContext.Categories,
                product => product.CategoryId,
                category => category.Id,
                (product, categories) => new { product, categories }
            )
            .SelectMany(
                x => x.categories.DefaultIfEmpty(),
                (x, category) =>
                    new
                    {
                        x.product.CategoryId,
                        CategoryName = category != null
                            ? category.Name
                            : ProductCategoryFacetValue.Uncategorized,
                    }
            )
            .GroupBy(x => new { x.CategoryId, x.CategoryName })
            .Select(group => new
            {
                group.Key.CategoryId,
                group.Key.CategoryName,
                Count = group.Count(),
            })
            .OrderByDescending(group => group.Count)
            .ThenBy(group => group.CategoryName)
            .Select(group => new ProductCategoryFacetValue(
                group.CategoryId,
                group.CategoryName,
                group.Count
            ))
            .ToArrayAsync(ct);
    }

    /// <summary>Returns fixed price bucket facet counts computed in a single server-side aggregate query.</summary>
    public async Task<IReadOnlyList<ProductPriceFacetBucketResponse>> GetPriceFacetsAsync(
        ProductFilter filter,
        CancellationToken ct = default
    )
    {
        ProductPriceFacetSpecification specification = new(filter);
        IQueryable<Product> query =
            Ardalis.Specification.EntityFrameworkCore.SpecificationEvaluator.Default.GetQuery(
                _dbContext.Products.AsQueryable(),
                specification
            );

        PriceFacetCounts? counts = await query
            .GroupBy(_ => 1)
            .Select(group => new PriceFacetCounts(
                group.Count(product => product.Price >= 0m && product.Price < 50m),
                group.Count(product => product.Price >= 50m && product.Price < 100m),
                group.Count(product => product.Price >= 100m && product.Price < 250m),
                group.Count(product => product.Price >= 250m && product.Price < 500m),
                group.Count(product => product.Price >= 500m)
            ))
            .SingleOrDefaultAsync(ct);

        int[] countArray = counts?.ToArray() ?? new int[DefaultPriceBuckets.Count];
        return DefaultPriceBuckets
            .Select(
                (bucket, i) => bucket with { Count = i < countArray.Length ? countArray[i] : 0 }
            )
            .ToArray();
    }

    /// <summary>
    ///     Bulk-sets <c>CategoryId</c> to <c>null</c> for all products whose <c>CategoryId</c> is in
    ///     <paramref name="categoryIds" />.
    /// </summary>
    public async Task ClearCategoryAsync(
        IReadOnlyCollection<Guid> categoryIds,
        CancellationToken ct = default
    )
    {
        await _dbContext
            .Products.Where(product =>
                product.CategoryId != null && categoryIds.Contains(product.CategoryId.Value)
            )
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(product => product.CategoryId, (Guid?)null),
                ct
            );
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> GetNonDeletedIdsByTenantAsync(
        Guid tenantId,
        CancellationToken ct = default
    )
    {
        return await _dbContext
            .Products.IgnoreQueryFilters()
            .Where(product => product.TenantId == tenantId && !product.IsDeleted)
            .Select(product => product.Id)
            .ToListAsync(ct);
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
            .Products.IgnoreQueryFilters()
            .Where(product => product.TenantId == tenantId && !product.IsDeleted)
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
            .Products.IgnoreQueryFilters()
            .Where(product =>
                product.TenantId == tenantId && ids.Contains(product.Id) && !product.IsDeleted
            )
            .BulkSoftDeleteAsync(actorId, deletedAtUtc, ct);
    }

    private sealed record PriceFacetCounts(
        int ZeroToFifty,
        int FiftyToOneHundred,
        int OneHundredToTwoHundredFifty,
        int TwoHundredFiftyToFiveHundred,
        int FiveHundredAndAbove
    )
    {
        public int[] ToArray()
        {
            return
            [
                ZeroToFifty,
                FiftyToOneHundred,
                OneHundredToTwoHundredFifty,
                TwoHundredFiftyToFiveHundred,
                FiveHundredAndAbove,
            ];
        }
    }
}
