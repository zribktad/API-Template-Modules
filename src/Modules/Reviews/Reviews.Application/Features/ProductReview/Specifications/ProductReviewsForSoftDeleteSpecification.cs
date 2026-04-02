using Ardalis.Specification;
using ProductReviewEntity = Reviews.Domain.Entities.ProductReview;

namespace Reviews.Application.Features.ProductReview.Specifications;

public sealed class ProductReviewsForSoftDeleteSpecification : Specification<ProductReviewEntity>
{
    public ProductReviewsForSoftDeleteSpecification(Guid productId)
    {
        Query.Where(review => review.ProductId == productId);
    }
}
