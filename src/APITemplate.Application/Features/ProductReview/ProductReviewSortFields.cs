using APITemplate.Application.Common.Sorting;
using ProductReviewEntity = APITemplate.Domain.Entities.ProductReview;

namespace APITemplate.Application.Features.ProductReview;

/// <summary>
/// Defines the allowed sort fields for product review queries and maps them to entity expressions.
/// </summary>
public static class ProductReviewSortFields
{
    /// <summary>Sort by review rating.</summary>
    public static readonly SortField Rating = new("rating");

    /// <summary>Sort by review creation timestamp.</summary>
    public static readonly SortField CreatedAt = new("createdAt");

    /// <summary>
    /// The sort field map used to resolve and apply sorting to product review specifications.
    /// Defaults to sorting by <see cref="CreatedAt"/> when no sort field is specified.
    /// </summary>
    public static readonly SortFieldMap<ProductReviewEntity> Map =
        new SortFieldMap<ProductReviewEntity>()
            .Add(Rating, r => (object)r.Rating)
            .Add(CreatedAt, r => r.Audit.CreatedAtUtc)
            .Default(r => r.Audit.CreatedAtUtc);
}
