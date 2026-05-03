using ErrorOr;
using ProductCatalog.Features.Category.GetCategoryById;
using ProductCatalog.Features.Category.Shared;
using Wolverine;

namespace ProductCatalog.GraphQL.DataLoaders;

/// <summary>
///     Hot Chocolate batch data loader that resolves categories by their IDs in a single query,
///     preventing the N+1 problem when the GraphQL schema resolves categories for products.
/// </summary>
public sealed class CategoryByIdDataLoader : BatchDataLoader<Guid, CategoryResponse>
{
    private readonly IMessageBus _bus;

    public CategoryByIdDataLoader(
        IMessageBus bus,
        IBatchScheduler batchScheduler,
        DataLoaderOptions options = default!
    )
        : base(batchScheduler, options)
    {
        _bus = bus;
    }

    /// <summary>
    ///     Fetches categories for the supplied <paramref name="keys" /> in one round-trip.
    /// </summary>
    protected override async Task<IReadOnlyDictionary<Guid, CategoryResponse>> LoadBatchAsync(
        IReadOnlyList<Guid> keys,
        CancellationToken ct
    )
    {
        ErrorOr<IReadOnlyDictionary<Guid, CategoryResponse>> result = await _bus.InvokeAsync<
            ErrorOr<IReadOnlyDictionary<Guid, CategoryResponse>>
        >(new GetCategoriesByIdsQuery(keys), ct);

        return result.ToGraphQLResult();
    }
}
