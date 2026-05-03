using System.ComponentModel.DataAnnotations;
using BuildingBlocks.Application.Validation;
using ProductCatalog.Features.Product.Shared;

namespace ProductCatalog.Features.Product.GetProducts;

/// <summary>
///     Encapsulates all criteria available for querying and paging the product list, including text search, price range,
///     date range, category filtering, and sorting.
/// </summary>
public sealed record ProductFilter(
    string? Name = null,
    string? Description = null,
    [Range(
        typeof(decimal),
        "0",
        "79228162514264337593543950335",
        ErrorMessage = "MinPrice must be greater than or equal to zero."
    )]
        decimal? MinPrice = null,
    [Range(
        typeof(decimal),
        "0",
        "79228162514264337593543950335",
        ErrorMessage = "MaxPrice must be greater than or equal to zero."
    )]
    [GreaterThanOrEqualToProperty(
        nameof(MinPrice),
        ErrorMessage = "MaxPrice must be greater than or equal to MinPrice."
    )]
        decimal? MaxPrice = null,
    DateTime? CreatedFrom = null,
    [GreaterThanOrEqualToProperty(
        nameof(CreatedFrom),
        ErrorMessage = "CreatedTo must be greater than or equal to CreatedFrom."
    )]
        DateTime? CreatedTo = null,
    [CaseInsensitiveAllowedValues(
        ProductSortFields.NameToken,
        ProductSortFields.PriceToken,
        ProductSortFields.CreatedAtToken,
        ErrorMessage = "SortBy must be one of: Name, Price, CreatedAt."
    )]
        string? SortBy = null,
    [SortDirection] string? SortDirection = null,
    [Range(1, int.MaxValue, ErrorMessage = PaginationFilter.PageNumberErrorMessage)]
        int PageNumber = 1,
    [Range(1, PaginationFilter.MaxPageSize, ErrorMessage = PaginationFilter.PageSizeErrorMessage)]
        int PageSize = PaginationFilter.DefaultPageSize,
    string? Query = null,
    [NoEmptyGuidItems] IReadOnlyCollection<Guid>? CategoryIds = null
) : PaginationFilter(PageNumber, PageSize), IDateRangeFilter, ISortableFilter;
