using ErrorOr;
using HotChocolate.Authorization;
using ProductCatalog.Features.Category.GetCategories;
using ProductCatalog.Features.Category.GetCategoryById;
using SharedKernel.Application.Validation;
using Wolverine;

namespace ProductCatalog.GraphQL.Queries;

/// <summary>
///     Hot Chocolate query type extension that adds category queries to the <see cref="ProductQueries" />
///     root, providing paginated list and single-item lookup operations.
/// </summary>
[Authorize]
[ExtendObjectType(typeof(ProductQueries))]
public sealed class CategoryQueries
{
    /// <summary>
    ///     Returns a paginated category list, mapping the GraphQL input to the application-layer
    ///     filter before dispatching via the message bus.
    /// </summary>
    public async Task<CategoryPageResult> GetCategories(
        CategoryQueryInput? input,
        [Service] IMessageBus bus,
        [Service] IValidator validator,
        CancellationToken ct
    )
    {
        CategoryFilter filter = (input ?? new CategoryQueryInput()).ToFilter();
        validator.ValidateForGraphQL(filter);
        ErrorOr<PagedResponse<CategoryResponse>> result = await bus.InvokeAsync<ErrorOr<PagedResponse<CategoryResponse>>>(
            new GetCategoriesQuery(filter),
            ct
        );
        return new CategoryPageResult(result.ToGraphQLResult());
    }

    /// <summary>Returns a single category by ID, or <see langword="null" /> if not found.</summary>
    public async Task<CategoryResponse?> GetCategoryById(
        Guid id,
        [Service] IMessageBus bus,
        CancellationToken ct
    )
    {
        ErrorOr<CategoryResponse> result = await bus.InvokeAsync<ErrorOr<CategoryResponse>>(
            new GetCategoryByIdQuery(id),
            ct
        );
        return result.ToGraphQLNullableResult();
    }
}
