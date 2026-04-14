using Microsoft.EntityFrameworkCore;
using Reviews.Persistence;

namespace Reviews.Repositories;

/// <summary>
///     EF Core repository for <see cref="ProductReview" />, inheriting all standard CRUD and specification query
///     support from <see cref="RepositoryBase{T}" />.
/// </summary>
public sealed class ProductReviewRepository
    : RepositoryBase<ProductReview>,
        IProductReviewRepository
{
    private readonly ReviewsDbContext _dbContext;

    public ProductReviewRepository(ReviewsDbContext dbContext)
        : base(dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<int> BulkSoftDeleteByProductIdsAsync(
        IReadOnlyList<Guid> productIds,
        Guid actorId,
        DateTime deletedAtUtc,
        CancellationToken ct = default
    )
    {
        return await _dbContext
            .ProductReviews.IgnoreQueryFilters()
            .Where(review => productIds.Contains(review.ProductId) && !review.IsDeleted)
            .BulkSoftDeleteAsync(actorId, deletedAtUtc, ct);
    }
}
