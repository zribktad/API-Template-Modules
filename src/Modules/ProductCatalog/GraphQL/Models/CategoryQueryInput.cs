using SharedKernel.Application.DTOs;

namespace ProductCatalog.GraphQL.Models;

/// <summary>
/// GraphQL input type for querying categories, providing optional text search,
/// sorting, and pagination parameters.
/// </summary>
public sealed class CategoryQueryInput
{
    public string? Query { get; init; }
    public string? SortBy { get; init; }
    public string? SortDirection { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = PaginationFilter.DefaultPageSize;
}




