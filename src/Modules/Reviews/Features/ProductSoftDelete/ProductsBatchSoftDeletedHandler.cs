using Wolverine;
using ProductReviewEntity = Reviews.Domain.ProductReview;

namespace Reviews.Features.ProductSoftDelete;

/// <summary>
///     Handles <see cref="ProductsBatchSoftDeletedNotification" /> by cascading the soft-delete
///     to all <see cref="ProductReviewEntity" /> records for the given product IDs in a single batch query.
/// </summary>
public static class ProductsBatchSoftDeletedHandler
{
    public static async Task<OutgoingMessages> Handle(
        ProductsBatchSoftDeletedNotification notification,
        IProductReviewRepository repository,
        IUnitOfWork<ReviewsDbMarker> unitOfWork,
        CancellationToken ct
    )
    {
        IReadOnlyList<ProductReviewEntity> reviews = await repository.ListAsync(
            new ProductReviewsForBatchSoftDeleteSpecification(notification.ProductIds),
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

        OutgoingMessages messages = [new CacheInvalidationNotification(CacheTags.Reviews)];
        return messages;
    }
}
