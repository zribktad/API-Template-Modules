using APITemplate.Application.Common.Search;
using Ardalis.Specification;
using Microsoft.EntityFrameworkCore;
using ProductEntity = APITemplate.Domain.Entities.Product;

namespace APITemplate.Application.Features.Product.Specifications;

/// <summary>
/// Internal helper that extends <see cref="ISpecificationBuilder{T}"/> with product-specific filter predicates, centralising all WHERE-clause logic for reuse across multiple specifications.
/// </summary>
internal static class ProductFilterCriteria
{
    /// <summary>
    /// Applies the active predicates from <paramref name="filter"/> to the specification builder, with optional overrides via <paramref name="options"/> to skip category-ID or price-range constraints when computing facets.
    /// </summary>
    internal static void ApplyFilter(
        this ISpecificationBuilder<ProductEntity> query,
        ProductFilter filter,
        ProductFilterCriteriaOptions? options = null
    )
    {
        options ??= ProductFilterCriteriaOptions.Default;

        if (!string.IsNullOrWhiteSpace(filter.Name))
            query.Where(p => p.Name.Contains(filter.Name));

        if (!string.IsNullOrWhiteSpace(filter.Description))
            query.Where(p => p.Description != null && p.Description.Contains(filter.Description));

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            query.Where(p =>
                EF.Functions.ToTsVector(
                        SearchDefaults.TextSearchConfiguration,
                        p.Name + " " + (p.Description ?? string.Empty)
                    )
                    .Matches(
                        EF.Functions.WebSearchToTsQuery(
                            SearchDefaults.TextSearchConfiguration,
                            filter.Query
                        )
                    )
            );
        }

        if (!options.IgnorePriceRange && filter.MinPrice.HasValue)
            query.Where(p => p.Price >= filter.MinPrice.Value);

        if (!options.IgnorePriceRange && filter.MaxPrice.HasValue)
            query.Where(p => p.Price <= filter.MaxPrice.Value);

        if (filter.CreatedFrom.HasValue)
            query.Where(p => p.Audit.CreatedAtUtc >= filter.CreatedFrom.Value);

        if (filter.CreatedTo.HasValue)
            query.Where(p => p.Audit.CreatedAtUtc <= filter.CreatedTo.Value);

        if (!options.IgnoreCategoryIds && filter.CategoryIds is { Count: > 0 })
            query.Where(p =>
                p.CategoryId.HasValue && filter.CategoryIds.Contains(p.CategoryId.Value)
            );
    }
}

/// <summary>
/// Controls which filter predicates are suppressed when building specifications for facet queries.
/// </summary>
internal sealed record ProductFilterCriteriaOptions(
    bool IgnoreCategoryIds = false,
    bool IgnorePriceRange = false
)
{
    internal static ProductFilterCriteriaOptions Default => new();
}
