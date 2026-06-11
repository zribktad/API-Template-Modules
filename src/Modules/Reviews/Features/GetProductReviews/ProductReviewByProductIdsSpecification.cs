using Ardalis.Specification;
using ProductReviewEntity = Reviews.Domain.ProductReview;

namespace Reviews.Features;

/// <summary>
///     Ardalis specification that retrieves reviews for a collection of product ids in a single query,
///     ordered by creation date descending and projected to <see cref="ProductReviewResponse" />.
/// </summary>
public sealed class ProductReviewByProductIdsSpecification
    : Specification<ProductReviewEntity, ProductReviewResponse>
{
    /// <summary>
    ///     Initialises the specification for the given set of <paramref name="productIds" />, bounding the
    ///     total rows fetched to <paramref name="maxPerProduct" /> × product count so a wide GraphQL query
    ///     (many products × all-reviews) cannot pull an unbounded result set. The per-product cap is
    ///     enforced by the caller after grouping.
    /// </summary>
    public ProductReviewByProductIdsSpecification(
        IReadOnlyCollection<Guid> productIds,
        int maxPerProduct
    )
    {
        Query
            .Where(r => productIds.Contains(r.ProductId))
            .OrderByDescending(r => r.Audit.CreatedAtUtc)
            .Take(productIds.Count * maxPerProduct)
            .Select(ProductReviewMappings.Projection);
    }
}
