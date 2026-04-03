using Ardalis.Specification;
using ProductReviewEntity = Reviews.Domain.ProductReview;

namespace Reviews.Features;

public sealed class ProductReviewsForSoftDeleteSpecification : Specification<ProductReviewEntity>
{
    public ProductReviewsForSoftDeleteSpecification(Guid productId)
    {
        Query.Where(review => review.ProductId == productId);
    }
}






