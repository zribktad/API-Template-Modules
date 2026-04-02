using ProductCatalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ProductApplicationRepository = ProductCatalog.Application.Features.Product.Repositories.IProductRepository;
using ProductDomainRepository = ProductCatalog.Domain.Interfaces.IProductRepository;

namespace ProductCatalog.Infrastructure.Repositories;

/// <summary>
/// EF Core repository for <see cref="Product"/> with specification-based listing,
/// count, category facet, and price bucket facet queries.
/// </summary>
public class ProductRepository : RepositoryBase<Product>, ProductApplicationRepository, ProductDomainRepository
{
    private readonly ProductCatalogDbContext _dbContext;

    private static readonly IReadOnlyList<ProductPriceFacetBucketResponse> DefaultPriceBuckets =
    [
        new("0 - 50", 0m, 50m, 0),
        new("50 - 100", 50m, 100m, 0),
        new("100 - 250", 100m, 250m, 0),
        new("250 - 500", 250m, 500m, 0),
        new("500+", 500m, null, 0),
    ];

    public ProductRepository(ProductCatalogDbContext dbContext)
        : base(dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>Returns a single-query paged result of products matching the given filter.</summary>
    public async Task<PagedResponse<ProductResponse>> GetPagedAsync(
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

    /// <summary>Returns category facet counts for products matching the filter, ordered by descending count then category name.</summary>
    public async Task<IReadOnlyList<ProductCategoryFacetValue>> GetCategoryFacetsAsync(
        ProductFilter filter,
        CancellationToken ct = default
    )
    {
        var specification = new ProductCategoryFacetSpecification(filter);
        var query =
            Ardalis.Specification.EntityFrameworkCore.SpecificationEvaluator.Default.GetQuery(
                _dbContext.Products.AsQueryable(),
                specification
            );

        return await query
            .GroupBy(product => new
            {
                product.CategoryId,
                CategoryName = product.Category != null ? product.Category.Name : "Uncategorized",
            })
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
        var specification = new ProductPriceFacetSpecification(filter);
        var query =
            Ardalis.Specification.EntityFrameworkCore.SpecificationEvaluator.Default.GetQuery(
                _dbContext.Products.AsQueryable(),
                specification
            );

        var counts = await query
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
            .Select((bucket, i) => bucket with { Count = i < countArray.Length ? countArray[i] : 0 })
            .ToArray();
    }

    private sealed record PriceFacetCounts(
        int ZeroToFifty,
        int FiftyToOneHundred,
        int OneHundredToTwoHundredFifty,
        int TwoHundredFiftyToFiveHundred,
        int FiveHundredAndAbove
    )
    {
        public int[] ToArray() =>
            [ZeroToFifty, FiftyToOneHundred, OneHundredToTwoHundredFifty, TwoHundredFiftyToFiveHundred, FiveHundredAndAbove];
    }
}
