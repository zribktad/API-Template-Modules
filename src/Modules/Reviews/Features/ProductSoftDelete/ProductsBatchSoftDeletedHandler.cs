using Wolverine;

namespace Reviews.Features.ProductSoftDelete;

/// <summary>
///     Handles <see cref="ProductsBatchSoftDeletedNotification" /> by cascading the soft-delete
///     to all <see cref="ProductReview" /> records for the given product IDs via a single bulk SQL
///     statement (<c>ExecuteUpdateAsync</c>) — zero entity materialization.
/// </summary>
public static class ProductsBatchSoftDeletedHandler
{
    public static async Task<OutgoingMessages> Handle(
        ProductsBatchSoftDeletedNotification notification,
        IProductReviewRepository repository,
        CancellationToken ct
    )
    {
        int affected = await repository.BulkSoftDeleteByProductIdsAsync(
            notification.ProductIds,
            notification.ActorId,
            notification.DeletedAtUtc,
            ct
        );

        if (affected == 0)
            return OutgoingMessagesHelper.Empty;

        OutgoingMessages messages = [new CacheInvalidationNotification(CacheTags.Reviews)];
        return messages;
    }
}
