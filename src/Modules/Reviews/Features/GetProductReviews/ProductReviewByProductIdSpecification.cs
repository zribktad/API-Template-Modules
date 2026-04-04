using Ardalis.Specification;
using ProductReviewEntity = Reviews.Domain.ProductReview;

namespace Reviews.Features;

/// <summary>
///     Ardalis specification that retrieves all reviews for a single product, ordered by creation date descending,
///     and projected directly to <see cref="ProductReviewResponse" />.
/// </summary>
public sealed class ProductReviewByProductIdSpecification
    : Specification<ProductReviewEntity, ProductReviewResponse>
{
    /// <summary>Initialises the specification for the given <paramref name="productId" />.</summary>
    public ProductReviewByProductIdSpecification(Guid productId)
    {
        Query
            .Where(r => r.ProductId == productId)
            .OrderByDescending(r => r.Audit.CreatedAtUtc)
            .Select(ProductReviewMappings.Projection);
    }
}
