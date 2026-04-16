using ErrorOr;
using HotChocolate.Authorization;
using Wolverine;

namespace ProductCatalog.GraphQL.Queries;

/// <summary>
///     Hot Chocolate query type extension that adds product-review queries to the
///     <see cref="ProductQueries" /> root, supporting filtered list, single-item, and
///     per-product lookup operations.
/// </summary>
[Authorize]
[ExtendObjectType(typeof(ProductQueries))]
public class ProductReviewQueries
{
    /// <summary>
    ///     Returns a paginated review list, mapping the GraphQL input to the application-layer
    ///     filter before dispatching via the message bus.
    /// </summary>
    public async Task<ProductReviewPageResult> GetReviews(
        ProductReviewQueryInput? input,
        [Service] IMessageBus bus,
        CancellationToken ct
    )
    {
        ProductReviewFilter filter = new()
        {
            ProductId = input?.ProductId,
            UserId = input?.UserId,
            MinRating = input?.MinRating,
            MaxRating = input?.MaxRating,
            CreatedFrom = input?.CreatedFrom,
            CreatedTo = input?.CreatedTo,
            SortBy = input?.SortBy,
            SortDirection = input?.SortDirection,
            PageNumber = input?.PageNumber ?? 1,
            PageSize = input?.PageSize ?? 20,
        };

        ErrorOr<PagedResponse<ProductReviewResponse>> result = await bus.InvokeAsync<
            ErrorOr<PagedResponse<ProductReviewResponse>>
        >(new GetProductReviewsQuery(filter), ct);
        return new ProductReviewPageResult(result.ToGraphQLResult());
    }

    /// <summary>Returns a single review by ID, or <see langword="null" /> if not found.</summary>
    public async Task<ProductReviewResponse?> GetReviewById(
        Guid id,
        [Service] IMessageBus bus,
        CancellationToken ct
    )
    {
        ErrorOr<ProductReviewResponse> result = await bus.InvokeAsync<
            ErrorOr<ProductReviewResponse>
        >(new GetProductReviewByIdQuery(id), ct);
        return result.ToGraphQLNullableResult();
    }

    /// <summary>Returns a paginated list of reviews scoped to a specific product.</summary>
    public async Task<ProductReviewPageResult> GetReviewsByProductId(
        Guid productId,
        int pageNumber,
        int pageSize,
        [Service] IMessageBus bus,
        CancellationToken ct
    )
    {
        ProductReviewFilter filter = new()
        {
            ProductId = productId,
            PageNumber = pageNumber,
            PageSize = pageSize,
        };
        ErrorOr<PagedResponse<ProductReviewResponse>> result = await bus.InvokeAsync<
            ErrorOr<PagedResponse<ProductReviewResponse>>
        >(new GetProductReviewsQuery(filter), ct);
        return new ProductReviewPageResult(result.ToGraphQLResult());
    }
}
