using BuildingBlocks.Application.Validation;
using ErrorOr;
using HotChocolate.Authorization;
using SharedKernel.Contracts.Queries.Reviews;
using Wolverine;

namespace Reviews.GraphQL.Mutations;

/// <summary>
///     Hot Chocolate mutation type that exposes product-review write operations
///     (create and delete).
/// </summary>
[Authorize]
[ExtendObjectType(HotChocolate.Types.OperationTypeNames.Mutation)]
public class ProductReviewMutations
{
    /// <summary>Creates a new product review and returns the persisted review.</summary>
    [Authorize(Policy = Permission.ProductReviews.Create)]
    public async Task<ProductReviewResponse> CreateProductReview(
        CreateProductReviewRequest input,
        [Service] IMessageBus bus,
        [Service] IValidator validator,
        CancellationToken ct
    )
    {
        validator.ValidateForGraphQL(input);
        ErrorOr<ProductReviewResponse> result = await bus.InvokeAsync<
            ErrorOr<ProductReviewResponse>
        >(new CreateProductReviewCommand(input), ct);
        return result.ToGraphQLResult();
    }

    /// <summary>Deletes a product review by its ID and returns <see langword="true" /> on success.</summary>
    [Authorize(Policy = Permission.ProductReviews.Delete)]
    public async Task<bool> DeleteProductReview(
        Guid id,
        [Service] IMessageBus bus,
        CancellationToken ct
    )
    {
        ErrorOr<Success> result = await bus.InvokeAsync<ErrorOr<Success>>(
            new DeleteProductReviewCommand(id),
            ct
        );
        return result.ToGraphQLResult() == Result.Success;
    }
}
