using APITemplate.Application.Features.ProductReview.Mappings;
using Ardalis.Specification;
using ProductReviewEntity = APITemplate.Domain.Entities.ProductReview;

namespace APITemplate.Application.Features.ProductReview.Specifications;

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
