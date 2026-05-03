using ErrorOr;
using ProductCatalog.Features.Category.Shared;
using ProductCatalog.Features.Product.GetProducts;
using ProductCatalog.Features.Product.Shared;
using ProductCatalog.GraphQL.Models;
using ProductCatalog.GraphQL.Queries;
using SharedKernel.Application.Validation;
using Wolverine;

namespace ProductCatalog.GraphQL.Types;

/// <summary>
///     Resolver class for fields on <see cref="CategoryType" />.
/// </summary>
public sealed class CategoryTypeResolvers
{
    /// <summary>
    ///     Loads products for the given <paramref name="category" /> via the message bus.
    /// </summary>
    public async Task<ProductPageResult> GetProducts(
        [Parent] CategoryResponse category,
        [Service] IMessageBus bus,
        [Service] IValidator validator,
        CancellationToken ct
    )
    {
        ProductFilter filter = new(CategoryIds: new[] { category.Id });
        validator.ValidateForGraphQL(filter);

        ErrorOr<ProductsResponse> result = await bus.InvokeAsync<ErrorOr<ProductsResponse>>(
            new GetProductsQuery(filter),
            ct
        );

        ProductsResponse page = result.ToGraphQLResult();
        return new ProductPageResult(page.Page, page.Facets);
    }
}
