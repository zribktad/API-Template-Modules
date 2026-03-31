using APITemplate.Application.Common.Search;
using Ardalis.Specification;
using Microsoft.EntityFrameworkCore;
using CategoryEntity = APITemplate.Domain.Entities.Category;

namespace APITemplate.Application.Features.Category.Specifications;

/// <summary>
/// Extension methods that apply <see cref="CategoryFilter"/> search criteria to an Ardalis specification builder.
/// Uses PostgreSQL full-text search (<c>to_tsvector</c> / <c>websearch_to_tsquery</c>) when a query term is present.
/// </summary>
internal static class CategoryFilterCriteria
{
    /// <summary>
    /// Appends a full-text search predicate to <paramref name="query"/> when <see cref="CategoryFilter.Query"/> is non-empty.
    /// Searches across the category name and description columns.
    /// </summary>
    internal static void ApplyFilter(
        this ISpecificationBuilder<CategoryEntity> query,
        CategoryFilter filter
    )
    {
        if (string.IsNullOrWhiteSpace(filter.Query))
            return;

        query.Where(category =>
            EF.Functions.ToTsVector(
                    SearchDefaults.TextSearchConfiguration,
                    category.Name + " " + (category.Description ?? string.Empty)
                )
                .Matches(
                    EF.Functions.WebSearchToTsQuery(
                        SearchDefaults.TextSearchConfiguration,
                        filter.Query
                    )
                )
        );
    }
}
