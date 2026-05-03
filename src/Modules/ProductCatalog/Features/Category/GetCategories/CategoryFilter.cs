using System.ComponentModel.DataAnnotations;
using ProductCatalog.Features.Category.Shared;
using SharedKernel.Application.Validation;

namespace ProductCatalog.Features.Category.GetCategories;

/// <summary>
///     Filter parameters for querying categories, supporting full-text search, sorting, and pagination.
/// </summary>
public sealed record CategoryFilter(
    string? Query = null,
    [CaseInsensitiveAllowedValues(
        CategorySortFields.NameToken,
        CategorySortFields.CreatedAtToken,
        ErrorMessage = "SortBy must be one of: Name, CreatedAt."
    )]
        string? SortBy = null,
    [SortDirection] string? SortDirection = null,
    [Range(1, int.MaxValue, ErrorMessage = "PageNumber must be greater than or equal to 1.")]
        int PageNumber = 1,
    [Range(1, PaginationFilter.MaxPageSize, ErrorMessage = "PageSize must be between 1 and 100.")]
        int PageSize = PaginationFilter.DefaultPageSize
) : PaginationFilter(PageNumber, PageSize), ISortableFilter;
