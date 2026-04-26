using ErrorOr;
using SharedKernel.Contracts.Queries.ProductCatalog;
using Wolverine;
using ProductReviewEntity = Reviews.Domain.ProductReview;

namespace Reviews.Features;

/// <summary>Creates a new product review for the authenticated user and returns the persisted representation.</summary>
public sealed record CreateProductReviewCommand(CreateProductReviewRequest Request);

/// <summary>Handles <see cref="CreateProductReviewCommand" />.</summary>
public sealed class CreateProductReviewCommandHandler
{
    public static async Task<(
        HandlerContinuation,
        CreateProductReviewState?,
        OutgoingMessages
    )> LoadAsync(
        CreateProductReviewCommand command,
        IMessageBus bus,
        IActorProvider actorProvider,
        CancellationToken ct
    )
    {
        Guid userId = actorProvider.ActorId;

        ErrorOr<Rating> rating = Rating.Create(command.Request.Rating);
        if (rating.IsError)
        {
            OutgoingMessages ratingFailure = new();
            ratingFailure.RespondToSender((ErrorOr<ProductReviewResponse>)rating.Errors);
            return (HandlerContinuation.Stop, null, ratingFailure);
        }

        ErrorOr<Success> productExists = await bus.InvokeAsync<ErrorOr<Success>>(
            new ValidateProductExistsQuery(command.Request.ProductId),
            ct
        );

        if (productExists.IsError)
        {
            OutgoingMessages failureMessages = new();
            failureMessages.RespondToSender(
                (ErrorOr<ProductReviewResponse>)
                    Reviews.Common.Errors.DomainErrors.Reviews.ProductNotFoundForReview(
                        command.Request.ProductId
                    )
            );
            return (HandlerContinuation.Stop, null, failureMessages);
        }

        return (
            HandlerContinuation.Continue,
            new CreateProductReviewState(
                command.Request.ProductId,
                userId,
                command.Request.Comment,
                rating.Value
            ),
            OutgoingMessagesHelper.Empty
        );
    }

    public static async Task<(ErrorOr<ProductReviewResponse>, OutgoingMessages)> HandleAsync(
        CreateProductReviewCommand command,
        CreateProductReviewState state,
        IProductReviewRepository reviewRepository,
        IUnitOfWork<ReviewsDbMarker> unitOfWork,
        CancellationToken ct
    )
    {
        ProductReviewEntity review = await unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                ProductReviewEntity entity = ProductReviewEntity.Create(
                    state.ProductId,
                    state.UserId,
                    state.Rating,
                    state.Comment
                );
                await reviewRepository.AddAsync(entity, ct);
                return entity;
            },
            ct
        );

        OutgoingMessages messages = new();
        messages.AddRange(CacheInvalidationCascades.ForReviewChange);
        return (review.ToResponse(), messages);
    }

    public sealed record CreateProductReviewState(
        Guid ProductId,
        Guid UserId,
        string? Comment,
        Rating Rating
    );
}
