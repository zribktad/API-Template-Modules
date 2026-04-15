using ProductReviewEntity = Reviews.Domain.ProductReview;

namespace Reviews.Domain;

/// <summary>
///     Defines the allowed sort fields for product review queries and maps them to entity expressions.
/// </summary>
public static class ProductReviewSortFields
{
    public const string RatingToken = nameof(Rating);
    public const string CreatedAtToken = nameof(CreatedAt);

    /// <summary>Sort by review rating.</summary>
    public static readonly SortField Rating = new(RatingToken);

    /// <summary>Sort by review creation timestamp.</summary>
    public static readonly SortField CreatedAt = new(CreatedAtToken);

    /// <summary>
    ///     The sort field map used to resolve and apply sorting to product review specifications.
    ///     Defaults to sorting by <see cref="CreatedAt" /> when no sort field is specified.
    /// </summary>
    public static readonly SortFieldMap<ProductReviewEntity> Map =
        new SortFieldMap<ProductReviewEntity>()
            .Add(Rating, r => r.Rating)
            .Add(CreatedAt, r => r.Audit.CreatedAtUtc)
            .Default(r => r.Audit.CreatedAtUtc);
}
