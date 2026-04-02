namespace APITemplate.Api.GraphQL.Models;

/// <summary>
/// GraphQL return type that wraps a paginated product-review result set, implementing
/// <see cref="IPagedItems{T}"/> for consistent schema paging fields.
/// </summary>
public sealed record ProductReviewPageResult(PagedResponse<ProductReviewResponse> Page)
    : IPagedItems<ProductReviewResponse>;
