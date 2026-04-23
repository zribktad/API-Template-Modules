using ErrorOr;
using HotChocolate.Authorization;
using ProductCatalog.Features.Product.GetProductById;
using ProductCatalog.Features.Product.GetProducts;
using Wolverine;

namespace ProductCatalog.GraphQL.Queries;

/// <summary>
///     Hot Chocolate root query type that exposes product list and single-product lookups,
///     serving as the extension base for <see cref="CategoryQueries" /> and <see cref="ProductReviewQueries" />.
/// </summary>
[Authorize]
public class ProductQueries
{
    /// <summary>
    ///     Returns a paginated product list with search facets, mapping the GraphQL input to the
    ///     application-layer filter before dispatching via the message bus.
    /// </summary>
    public Task<ProductPageResult> GetProducts(
        ProductQueryInput? input,
        [Service] IMessageBus bus,
        CancellationToken ct
    ) => bus.InvokeAsync<ProductPageResult>(new GetProductsQuery((input ?? new ProductQueryInput()).ToFilter()), ct);

    /// <summary>Returns a single product by ID, or <see langword="null" /> if not found.</summary>
    public async Task<ProductResponse?> GetProductById(
        Guid id,
        [Service] IMessageBus bus,
        CancellationToken ct
    )
    {
        ErrorOr<ProductResponse> result = await bus.InvokeAsync<ErrorOr<ProductResponse>>(
            new GetProductByIdQuery(id),
            ct
        );
        return result.ToGraphQLNullableResult();
    }
}
