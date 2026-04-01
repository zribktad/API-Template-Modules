using ErrorOr;
using Wolverine;

namespace Reviews.Application.Features.ProductReview;

/// <summary>Deletes the product review with the given identifier; only the review's author may delete it.</summary>
public sealed record DeleteProductReviewCommand(Guid Id) : IHasId;

/// <summary>Handles <see cref="DeleteProductReviewCommand"/>.</summary>
public sealed class DeleteProductReviewCommandHandler
{
    public static async Task<(
        HandlerContinuation,
        Reviews.Domain.Entities.ProductReview?,
        OutgoingMessages
    )> LoadAsync(
        DeleteProductReviewCommand command,
        IProductReviewRepository reviewRepository,
        IActorProvider actorProvider,
        CancellationToken ct
    )
    {
        Guid userId = actorProvider.ActorId;
        ErrorOr<Reviews.Domain.Entities.ProductReview> reviewResult = await reviewRepository.GetByIdOrError(
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

        Reviews.Domain.Entities.ProductReview review = reviewResult.Value;

        if (review.UserId != userId)
        {
            OutgoingMessages failureMessages = new();
            failureMessages.RespondToSender(DomainErrors.Reviews.ForbiddenOwnReviewsOnly());
            return (HandlerContinuation.Stop, null, failureMessages);
        }

        return (HandlerContinuation.Continue, review, new OutgoingMessages());
    }

    public static async Task<(ErrorOr<Success>, OutgoingMessages)> HandleAsync(
        DeleteProductReviewCommand command,
        Reviews.Domain.Entities.ProductReview review,
        IProductReviewRepository reviewRepository,
        IUnitOfWork unitOfWork,
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
        messages.Add(new CacheInvalidationNotification(CacheTags.Reviews));
        messages.Add(new CacheInvalidationNotification(CacheTags.Categories));
        return (Result.Success, messages);
    }
}



