using Ardalis.Specification;
using Reviews.Domain;
using ProductReviewEntity = Reviews.Domain.ProductReview;

namespace Reviews.Features;

/// <summary>
/// Ardalis specification for querying a filtered and sorted list of product reviews
/// projected to <see cref="ProductReviewResponse"/>.
/// </summary>
public sealed class ProductReviewSpecification
    : Specification<ProductReviewEntity, ProductReviewResponse>
{
    /// <summary>Initialises the specification by applying filter, sort, and projection from <paramref name="filter"/>.</summary>
    public ProductReviewSpecification(ProductReviewFilter filter)
    {
        Query.ApplyFilter(filter);

        ProductReviewSortFields.Map.ApplySort(Query, filter.SortBy, filter.SortDirection);

        Query.Select(ProductReviewMappings.Projection);
    }
}
