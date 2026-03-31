using Reviews.Application.Features.ProductReview.Specifications;
using Wolverine;
using ProductReviewEntity = Reviews.Domain.Entities.ProductReview;

namespace Reviews.Application.Features.ProductReview;

public static class ProductSoftDeletedEventHandler
{
    public static async Task Handle(
        ProductSoftDeletedNotification notification,
        IProductReviewRepository repository,
        IUnitOfWork unitOfWork,
        IMessageBus bus,
        CancellationToken ct
    )
    {
        IReadOnlyList<ProductReviewEntity> reviews = await repository.ListAsync(
            new ProductReviewsForSoftDeleteSpecification(notification.ProductId),
            ct
        );

        if (reviews.Count == 0)
            return;

        await unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                await repository.DeleteRangeAsync(reviews, ct);
            },
            ct
        );

        await bus.PublishAsync(new CacheInvalidationNotification(CacheTags.Reviews));
    }
}
