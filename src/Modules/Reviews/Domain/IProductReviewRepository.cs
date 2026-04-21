namespace Reviews.Domain;

/// <summary>
///     Repository contract for <see cref="ProductReview" /> entities, inheriting all generic CRUD operations from
///     <see cref="IRepository{T}" />.
/// </summary>
public interface IProductReviewRepository : IRepository<ProductReview>
{
    /// <summary>
    ///     Bulk soft-deletes reviews for the specified product IDs within the given tenant via a
    ///     single <c>ExecuteUpdateAsync</c> SQL statement (zero entity materialization). Callers
    ///     supply <paramref name="tenantId" /> explicitly because this method is invoked from
    ///     Wolverine notification handlers that run on durable-local-queue background workers
    ///     without an HTTP scope, so <c>ITenantProvider.HasTenant</c> is <c>false</c> and the
    ///     global tenant filter is bypassed via <c>IgnoreQueryFilters()</c>.
    /// </summary>
    Task<int> BulkSoftDeleteByProductIdsAsync(
        IReadOnlyList<Guid> productIds,
        Guid tenantId,
        Guid actorId,
        DateTime deletedAtUtc,
        CancellationToken ct = default
    );
}
