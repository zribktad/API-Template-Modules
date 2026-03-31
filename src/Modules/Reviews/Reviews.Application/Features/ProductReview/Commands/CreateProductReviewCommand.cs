using SharedKernel.Application.Context;
using SharedKernel.Application.Errors;
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
    public static async Task<ErrorOr<ProductReviewResponse>> HandleAsync(
        CreateProductReviewCommand command,
        IProductReviewRepository reviewRepository,
        IProductRepository productRepository,
        IUnitOfWork unitOfWork,
        IActorProvider actorProvider,
        IMessageBus bus,
        CancellationToken ct
    )
    {
        var userId = actorProvider.ActorId;
        var productResult = await productRepository.GetByIdOrError(
            command.Request.ProductId,
            DomainErrors.Reviews.ProductNotFoundForReview(command.Request.ProductId),
            ct
        );
        if (productResult.IsError)
            return productResult.Errors;

        var review = await unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                var entity = new ProductReviewEntity
                {
                    Id = Guid.NewGuid(),
                    ProductId = command.Request.ProductId,
                    UserId = userId,
                    Comment = command.Request.Comment,
                    Rating = command.Request.Rating,
                };

                await reviewRepository.AddAsync(entity, ct);
                return entity;
            },
            ct
        );

        await bus.PublishAsync(new CacheInvalidationNotification(CacheTags.Reviews));
        return review.ToResponse();
    }
}



