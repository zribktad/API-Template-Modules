using ErrorOr;
using Wolverine;

namespace APITemplate.Api.GraphQL.DataLoaders;

/// <summary>
/// Hot Chocolate batch data loader that resolves all reviews for a set of product IDs in a
/// single query, preventing the N+1 problem when the GraphQL schema resolves reviews
/// as a field on <c>ProductType</c>.
/// </summary>
public sealed class ProductReviewsByProductDataLoader
    : BatchDataLoader<Guid, ProductReviewResponse[]>
{
    private readonly IMessageBus _bus;

    public ProductReviewsByProductDataLoader(
        IMessageBus bus,
        IBatchScheduler batchScheduler,
        DataLoaderOptions options = default!
    )
        : base(batchScheduler, options)
    {
        _bus = bus;
    }

    /// <summary>
    /// Fetches all reviews for the supplied <paramref name="productIds"/> in one round-trip
    /// and returns a dictionary keyed by product ID.
    /// </summary>
    protected override async Task<
        IReadOnlyDictionary<Guid, ProductReviewResponse[]>
    > LoadBatchAsync(IReadOnlyList<Guid> productIds, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<
            ErrorOr<IReadOnlyDictionary<Guid, ProductReviewResponse[]>>
        >(new GetProductReviewsByProductIdsQuery(productIds), ct);

        return result.ToGraphQLResult();
    }
}
