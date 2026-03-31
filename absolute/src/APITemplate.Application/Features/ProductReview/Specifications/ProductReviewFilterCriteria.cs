using Ardalis.Specification;
using ProductReviewEntity = APITemplate.Domain.Entities.ProductReview;

namespace APITemplate.Application.Features.ProductReview.Specifications;

/// <summary>
/// Extension methods that apply <see cref="ProductReviewFilter"/> criteria to an Ardalis specification builder.
/// Each filter field is applied conditionally, only when a value is present.
/// </summary>
internal static class ProductReviewFilterCriteria
{
    /// <summary>
    /// Appends filter predicates to <paramref name="query"/> for each non-null field in <paramref name="filter"/>,
    /// including product id, user id, rating range, and creation date range.
    /// </summary>
    internal static void ApplyFilter(
        this ISpecificationBuilder<ProductReviewEntity> query,
        ProductReviewFilter filter
    )
    {
        if (filter.ProductId.HasValue)
            query.Where(r => r.ProductId == filter.ProductId.Value);

        if (filter.UserId.HasValue)
            query.Where(r => r.UserId == filter.UserId.Value);

        if (filter.MinRating.HasValue)
            query.Where(r => r.Rating >= filter.MinRating.Value);

        if (filter.MaxRating.HasValue)
            query.Where(r => r.Rating <= filter.MaxRating.Value);

        if (filter.CreatedFrom.HasValue)
            query.Where(r => r.Audit.CreatedAtUtc >= filter.CreatedFrom.Value);

        if (filter.CreatedTo.HasValue)
            query.Where(r => r.Audit.CreatedAtUtc <= filter.CreatedTo.Value);
    }
}
