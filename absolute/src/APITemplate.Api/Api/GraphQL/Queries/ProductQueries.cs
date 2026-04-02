using APITemplate.Api.GraphQL.Models;
using ErrorOr;
using HotChocolate.Authorization;
using Wolverine;

namespace APITemplate.Api.GraphQL.Queries;

/// <summary>
/// Hot Chocolate root query type that exposes product list and single-product lookups,
/// serving as the extension base for <see cref="CategoryQueries"/> and <see cref="ProductReviewQueries"/>.
/// </summary>
[Authorize]
public class ProductQueries
{
    /// <summary>
    /// Returns a paginated product list with search facets, mapping the GraphQL input to the
    /// application-layer filter before dispatching via the message bus.
    /// </summary>
    public async Task<ProductPageResult> GetProducts(
        ProductQueryInput? input,
        [Service] IMessageBus bus,
        CancellationToken ct
    )
    {
        var filter = new ProductFilter(
            input?.Name,
            input?.Description,
            input?.MinPrice,
            input?.MaxPrice,
            input?.CreatedFrom,
            input?.CreatedTo,
            input?.SortBy,
            input?.SortDirection,
            input?.PageNumber ?? 1,
            input?.PageSize ?? PaginationFilter.DefaultPageSize,
            input?.Query,
            input?.CategoryIds
        );

        var result = await bus.InvokeAsync<ErrorOr<ProductsResponse>>(
            new GetProductsQuery(filter),
            ct
        );
        var page = result.ToGraphQLResult();
        return new ProductPageResult(page.Page, page.Facets);
    }

    /// <summary>Returns a single product by ID, or <see langword="null"/> if not found.</summary>
    public async Task<ProductResponse?> GetProductById(
        Guid id,
        [Service] IMessageBus bus,
        CancellationToken ct
    )
    {
        var result = await bus.InvokeAsync<ErrorOr<ProductResponse>>(
            new GetProductByIdQuery(id),
            ct
        );
        return result.ToGraphQLNullableResult();
    }
}
