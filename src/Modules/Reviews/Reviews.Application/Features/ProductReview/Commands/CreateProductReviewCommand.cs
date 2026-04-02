using Contracts.Events;
using Contracts.Queries.ProductCatalog;
using ErrorOr;
using Reviews.Application.Features.ProductReview.Mappings;
using Reviews.Domain;
using Reviews.Domain.Interfaces;
using Reviews.Domain.ValueObjects;
using SharedKernel.Application.Context;
using Wolverine;
using ProductReviewEntity = Reviews.Domain.Entities.ProductReview;

namespace Reviews.Application.Features.ProductReview;

/// <summary>Creates a new product review for the authenticated user and returns the persisted representation.</summary>
public sealed record CreateProductReviewCommand(CreateProductReviewRequest Request);

/// <summary>Handles <see cref="CreateProductReviewCommand"/>.</summary>
public sealed class CreateProductReviewCommandHandler
{
    public sealed record CreateProductReviewState(
        Guid ProductId,
        Guid UserId,
        string? Comment,
        Rating Rating
    );

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
            ratingFailure.RespondToSender(rating.Errors);
            return (HandlerContinuation.Stop, null, ratingFailure);
        }

        ErrorOr<Success> productExists = await bus.InvokeAsync<ErrorOr<Success>>(
            new ValidateProductExistsQuery(command.Request.ProductId),
            ct
        );
        if (productExists.IsError)
        {
            OutgoingMessages failureMessages = new();
            failureMessages.RespondToSender(productExists.Errors);
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
                ProductReviewEntity entity = new()
                {
                    Id = Guid.NewGuid(),
                    ProductId = state.ProductId,
                    UserId = state.UserId,
                    Comment = state.Comment,
                    Rating = state.Rating,
                };

                await reviewRepository.AddAsync(entity, ct);
                return entity;
            },
            ct
        );

        OutgoingMessages messages = new();
        messages.Add(new CacheInvalidationNotification(CacheTags.Reviews));
        messages.Add(new CacheInvalidationNotification(CacheTags.Categories));
        return (review.ToResponse(), messages);
    }
}
