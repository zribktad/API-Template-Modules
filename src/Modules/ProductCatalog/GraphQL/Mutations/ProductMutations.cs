using BuildingBlocks.Application.Validation;
using ErrorOr;
using HotChocolate.Authorization;
using ProductCatalog.Features.Product.CreateProducts;
using ProductCatalog.Features.Product.DeleteProducts;
using Wolverine;

namespace ProductCatalog.GraphQL.Mutations;

/// <summary>
///     Hot Chocolate mutation type that exposes product write operations backed by
///     batch CQRS handlers, enforcing per-operation authorization policies.
/// </summary>
[Authorize]
[ExtendObjectType(HotChocolate.Types.OperationTypeNames.Mutation)]
public class ProductMutations
{
    /// <summary>Creates one or more products and returns a batch outcome.</summary>
    [Authorize(Policy = Permission.Products.Create)]
    public async Task<BatchResponse> CreateProducts(
        CreateProductsRequest input,
        [Service] IMessageBus bus,
        [Service] IValidator validator,
        CancellationToken ct
    )
    {
        validator.ValidateForGraphQL(input);
        ErrorOr<BatchResponse> result = await bus.InvokeAsync<ErrorOr<BatchResponse>>(
            new CreateProductsCommand(input),
            ct
        );
        return result.ToGraphQLResult();
    }

    /// <summary>Deletes a single product by ID and returns <see langword="true" /> on success.</summary>
    [Authorize(Policy = Permission.Products.Delete)]
    public async Task<bool> DeleteProduct(Guid id, [Service] IMessageBus bus, CancellationToken ct)
    {
        ErrorOr<BatchResponse> result = await bus.InvokeAsync<ErrorOr<BatchResponse>>(
            new DeleteProductsCommand(new BatchDeleteRequest([id])),
            ct
        );
        BatchResponse batch = result.ToGraphQLResult();
        if (batch.FailureCount > 0)
        {
            BatchResultItem failure = batch.Failures[0];
            // Emit structured GraphQL errors with a code instead of a raw concatenated message.
            throw new GraphQLException(
                failure.Errors.Select(message =>
                    HotChocolate
                        .ErrorBuilder.New()
                        .SetMessage(message)
                        .SetCode("PRODUCT_DELETE_FAILED")
                        .SetExtension("index", failure.Index)
                        .Build()
                )
            );
        }

        return true;
    }

    /// <summary>Deletes one or more products and returns a batch outcome.</summary>
    [Authorize(Policy = Permission.Products.Delete)]
    public async Task<BatchResponse> DeleteProducts(
        BatchDeleteRequest input,
        [Service] IMessageBus bus,
        [Service] IValidator validator,
        CancellationToken ct
    )
    {
        validator.ValidateForGraphQL(input);
        ErrorOr<BatchResponse> result = await bus.InvokeAsync<ErrorOr<BatchResponse>>(
            new DeleteProductsCommand(input),
            ct
        );
        return result.ToGraphQLResult();
    }
}
