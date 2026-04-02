using APITemplate.Application.Features.ProductReview.Mappings;
using Ardalis.Specification;
using ProductReviewEntity = APITemplate.Domain.Entities.ProductReview;

namespace APITemplate.Application.Features.ProductReview.Specifications;

/// <summary>
/// Ardalis specification that retrieves reviews for a collection of product ids in a single query,
/// ordered by creation date descending and projected to <see cref="ProductReviewResponse"/>.
/// </summary>
public sealed class ProductReviewByProductIdsSpecification
    : Specification<ProductReviewEntity, ProductReviewResponse>
{
    /// <summary>Initialises the specification for the given set of <paramref name="productIds"/>.</summary>
    public ProductReviewByProductIdsSpecification(IReadOnlyCollection<Guid> productIds)
    {
        Query
            .Where(r => productIds.Contains(r.ProductId))
            .OrderByDescending(r => r.Audit.CreatedAtUtc)
            .Select(ProductReviewMappings.Projection);
    }
}
