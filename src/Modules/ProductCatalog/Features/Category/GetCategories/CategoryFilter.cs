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
    [Range(1, int.MaxValue, ErrorMessage = PaginationFilter.PageNumberErrorMessage)]
        int PageNumber = 1,
    [Range(1, PaginationFilter.MaxPageSize, ErrorMessage = PaginationFilter.PageSizeErrorMessage)]
        int PageSize = PaginationFilter.DefaultPageSize
) : PaginationFilter(PageNumber, PageSize), ISortableFilter;
