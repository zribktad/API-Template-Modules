using APITemplate.Domain.Entities;
using APITemplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace APITemplate.Infrastructure.Repositories;

/// <summary>
/// EF Core repository for <see cref="Product"/> with specification-based listing,
/// count, category facet, and price bucket facet queries.
/// </summary>
public class ProductRepository : RepositoryBase<Product>, IProductRepository
{
    private static readonly IReadOnlyList<ProductPriceFacetBucketResponse> DefaultPriceBuckets =
    [
        new("0 - 50", 0m, 50m, 0),
        new("50 - 100", 50m, 100m, 0),
        new("100 - 250", 100m, 250m, 0),
        new("250 - 500", 250m, 500m, 0),
        new("500+", 500m, null, 0),
    ];

    public ProductRepository(AppDbContext dbContext)
        : base(dbContext) { }

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
                AppDb.Products.AsQueryable(),
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
                AppDb.Products.AsQueryable(),
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

        return DefaultPriceBuckets
            .Select(bucket =>
                bucket with
                {
                    Count = bucket.Label switch
                    {
                        "0 - 50" => counts?.ZeroToFifty ?? 0,
                        "50 - 100" => counts?.FiftyToOneHundred ?? 0,
                        "100 - 250" => counts?.OneHundredToTwoHundredFifty ?? 0,
                        "250 - 500" => counts?.TwoHundredFiftyToFiveHundred ?? 0,
                        "500+" => counts?.FiveHundredAndAbove ?? 0,
                        _ => 0,
                    },
                }
            )
            .ToArray();
    }

    private sealed record PriceFacetCounts(
        int ZeroToFifty,
        int FiftyToOneHundred,
        int OneHundredToTwoHundredFifty,
        int TwoHundredFiftyToFiveHundred,
        int FiveHundredAndAbove
    );
}
