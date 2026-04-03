using Reviews.Features;
using Reviews;
using Wolverine;
using ProductReviewEntity = Reviews.Domain.ProductReview;

namespace Reviews.Features;

public static class ProductSoftDeletedEventHandler
{
    public static async Task<OutgoingMessages> Handle(
        ProductSoftDeletedNotification notification,
        IProductReviewRepository repository,
        IUnitOfWork<ReviewsDbMarker> unitOfWork,
        CancellationToken ct
    )
    {
        IReadOnlyList<ProductReviewEntity> reviews = await repository.ListAsync(
            new ProductReviewsForSoftDeleteSpecification(notification.ProductId),
            ct
        );

        if (reviews.Count == 0)
            return OutgoingMessagesHelper.Empty;

        await unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                await repository.DeleteRangeAsync(reviews, ct);
            },
            ct
        );

        OutgoingMessages messages = new();
        messages.Add(new CacheInvalidationNotification(CacheTags.Reviews));
        return messages;
    }
}






