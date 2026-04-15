using System.ComponentModel.DataAnnotations;
using SharedKernel.Application.Validation;

namespace ProductCatalog.Features.Category.GetCategories;

/// <summary>
///     Filter parameters for querying categories, supporting full-text search, sorting, and pagination.
/// </summary>
public sealed record CategoryFilter(
    string? Query = null,
    [AllowedValues("name", "createdAt", ErrorMessage = "SortBy must be one of: name, createdAt.")]
    string? SortBy = null,
    [SortDirection]
    string? SortDirection = null,
    int PageNumber = 1,
    int PageSize = PaginationFilter.DefaultPageSize
) : PaginationFilter(PageNumber, PageSize), ISortableFilter;
