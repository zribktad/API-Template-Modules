using ErrorOr;
using Wolverine;

namespace Reviews.Features;

/// <summary>Deletes the product review with the given identifier; only the review's author may delete it.</summary>
public sealed record DeleteProductReviewCommand(Guid Id) : IHasId;

/// <summary>Handles <see cref="DeleteProductReviewCommand" />.</summary>
public sealed class DeleteProductReviewCommandHandler
{
    public static async Task<(HandlerContinuation, ProductReview?, OutgoingMessages)> LoadAsync(
        DeleteProductReviewCommand command,
        IProductReviewRepository reviewRepository,
        IActorProvider actorProvider,
        CancellationToken ct
    )
    {
        Guid userId = actorProvider.ActorId;
        ErrorOr<ProductReview> reviewResult = await reviewRepository.GetByIdOrError(
            command.Id,
            DomainErrors.Reviews.NotFound(command.Id),
            ct
        );
        if (reviewResult.IsError)
        {
            OutgoingMessages failureMessages = new();
            failureMessages.RespondToSender(reviewResult.Errors);
            return (HandlerContinuation.Stop, null, failureMessages);
        }

        ProductReview review = reviewResult.Value;

        if (review.UserId != userId)
        {
            OutgoingMessages failureMessages = new();
            failureMessages.RespondToSender(DomainErrors.Reviews.ForbiddenOwnReviewsOnly());
            return (HandlerContinuation.Stop, null, failureMessages);
        }

        return (HandlerContinuation.Continue, review, OutgoingMessagesHelper.Empty);
    }

    public static async Task<(ErrorOr<Success>, OutgoingMessages)> HandleAsync(
        DeleteProductReviewCommand command,
        ProductReview review,
        IProductReviewRepository reviewRepository,
        IUnitOfWork<ReviewsDbMarker> unitOfWork,
        CancellationToken ct
    )
    {
        await unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                await reviewRepository.DeleteAsync(review, ct);
            },
            ct
        );

        OutgoingMessages messages = new();
        messages.AddRange(CacheInvalidationCascades.ForReviewChange(review.TenantId));
        return (Result.Success, messages);
    }
}
