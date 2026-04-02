using APITemplate.Application.Common.DTOs;

namespace APITemplate.Api.GraphQL.Models;

/// <summary>
/// GraphQL input type for querying products, supporting full-text search, price and date
/// range filters, category constraints, sorting, and pagination.
/// </summary>
public sealed class ProductQueryInput
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public DateTime? CreatedFrom { get; init; }
    public DateTime? CreatedTo { get; init; }
    public string? SortBy { get; init; }
    public string? SortDirection { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = PaginationFilter.DefaultPageSize;
    public string? Query { get; init; }
    public IReadOnlyCollection<Guid>? CategoryIds { get; init; }
}
