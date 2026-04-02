namespace APITemplate.Api.GraphQL.Models;

/// <summary>
/// GraphQL return type that wraps a paginated category result set, implementing
/// <see cref="IPagedItems{T}"/> so the schema exposes consistent paging fields.
/// </summary>
public sealed record CategoryPageResult(PagedResponse<CategoryResponse> Page)
    : IPagedItems<CategoryResponse>;
