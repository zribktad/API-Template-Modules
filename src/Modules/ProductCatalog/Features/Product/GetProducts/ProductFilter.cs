using System.ComponentModel.DataAnnotations;
using SharedKernel.Application.Validation;

namespace ProductCatalog.Features.Product.GetProducts;

/// <summary>
///     Encapsulates all criteria available for querying and paging the product list, including text search, price range,
///     date range, category filtering, and sorting.
/// </summary>
public sealed record ProductFilter(
    string? Name = null,
    string? Description = null,
    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "MinPrice must be greater than or equal to zero.")]
    decimal? MinPrice = null,
    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "MaxPrice must be greater than or equal to zero.")]
    decimal? MaxPrice = null,
    DateTime? CreatedFrom = null,
    DateTime? CreatedTo = null,
    [AllowedValues("name", "price", "createdAt", ErrorMessage = "SortBy must be one of: name, price, createdAt.")]
    string? SortBy = null,
    [SortDirection]
    string? SortDirection = null,
    int PageNumber = 1,
    int PageSize = PaginationFilter.DefaultPageSize,
    string? Query = null,
    [NoEmptyGuidItems]
    IReadOnlyCollection<Guid>? CategoryIds = null
) : PaginationFilter(PageNumber, PageSize), IDateRangeFilter, ISortableFilter;
