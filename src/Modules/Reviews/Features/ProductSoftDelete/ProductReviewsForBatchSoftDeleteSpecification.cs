using Ardalis.Specification;
using ProductReviewEntity = Reviews.Domain.ProductReview;

namespace Reviews.Features.ProductSoftDelete;

public sealed class ProductReviewsForBatchSoftDeleteSpecification : Specification<ProductReviewEntity>
{
    public ProductReviewsForBatchSoftDeleteSpecification(IReadOnlyList<Guid> productIds)
    {
        Query
            .Where(review => productIds.Contains(review.ProductId) && !review.IsDeleted)
            .IgnoreQueryFilters();
    }
}
