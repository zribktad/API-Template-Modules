using SharedKernel.Application.Context;
using Contracts.Events;
using SharedKernel.Application.Extensions;
using ProductCatalog.Domain.Interfaces;
using Reviews.Application.Features.ProductReview.Mappings;
using Reviews.Domain.Interfaces;
using ErrorOr;
using Wolverine;
using ProductReviewEntity = Reviews.Domain.Entities.ProductReview;

namespace Reviews.Application.Features.ProductReview;

/// <summary>Creates a new product review for the authenticated user and returns the persisted representation.</summary>
public sealed record CreateProductReviewCommand(CreateProductReviewRequest Request);

/// <summary>Handles <see cref="CreateProductReviewCommand"/>.</summary>
public sealed class CreateProductReviewCommandHandler
{
    public sealed record CreateProductReviewState(Guid ProductId, Guid UserId, string? Comment, int Rating);

    public static async Task<(
        HandlerContinuation,
        CreateProductReviewState?,
        OutgoingMessages
    )> LoadAsync(
        CreateProductReviewCommand command,
        IProductRepository productRepository,
        IActorProvider actorProvider,
        CancellationToken ct
    )
    {
        Guid userId = actorProvider.ActorId;
        ErrorOr<ProductCatalog.Domain.Entities.Product> productResult = await productRepository.GetByIdOrError(
            command.Request.ProductId,
            DomainErrors.Reviews.ProductNotFoundForReview(command.Request.ProductId),
            ct
        );
        if (productResult.IsError)
        {
            OutgoingMessages failureMessages = new();
            failureMessages.RespondToSender(productResult.Errors);
            return (HandlerContinuation.Stop, null, failureMessages);
        }

        return (
            HandlerContinuation.Continue,
            new CreateProductReviewState(
                command.Request.ProductId,
                userId,
                command.Request.Comment,
                command.Request.Rating
            ),
            new OutgoingMessages()
        );
    }

    public static async Task<(ErrorOr<ProductReviewResponse>, OutgoingMessages)> HandleAsync(
        CreateProductReviewCommand command,
        CreateProductReviewState state,
        IProductReviewRepository reviewRepository,
        IUnitOfWork unitOfWork,
        CancellationToken ct
    )
    {
        var review = await unitOfWork.ExecuteInTransactionAsync(
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



