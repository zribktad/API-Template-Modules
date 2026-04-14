namespace Reviews.Domain;

/// <summary>
///     Repository contract for <see cref="ProductReview" /> entities, inheriting all generic CRUD operations from
///     <see cref="IRepository{T}" />.
/// </summary>
public interface IProductReviewRepository : IRepository<ProductReview>
{
    /// <summary>
    ///     Bulk soft-deletes all non-deleted reviews for the specified product IDs via a single
    ///     <c>ExecuteUpdateAsync</c> SQL statement (zero entity materialization).
    /// </summary>
    Task<int> BulkSoftDeleteByProductIdsAsync(
        IReadOnlyList<Guid> productIds,
        Guid actorId,
        DateTime deletedAtUtc,
        CancellationToken ct = default
    );
}
