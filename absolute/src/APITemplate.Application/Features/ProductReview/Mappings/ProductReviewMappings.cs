using System.Linq.Expressions;
using ProductReviewEntity = APITemplate.Domain.Entities.ProductReview;

namespace APITemplate.Application.Features.ProductReview.Mappings;

/// <summary>
/// Provides mapping utilities between product review domain entities and their response DTOs.
/// The compiled projection is shared across specifications and in-memory conversions.
/// </summary>
public static class ProductReviewMappings
{
    /// <summary>
    /// EF Core-compatible expression that projects a <see cref="ProductReviewEntity"/> to a <see cref="ProductReviewResponse"/>.
    /// Shared with specifications to ensure a consistent shape from both DB queries and entity-to-DTO conversions.
    /// </summary>
    public static readonly Expression<Func<ProductReviewEntity, ProductReviewResponse>> Projection =
        r => new ProductReviewResponse(
            r.Id,
            r.ProductId,
            r.UserId,
            r.Comment,
            r.Rating,
            r.Audit.CreatedAtUtc
        );

    private static readonly Func<ProductReviewEntity, ProductReviewResponse> CompiledProjection =
        Projection.Compile();

    /// <summary>Maps a <see cref="ProductReviewEntity"/> to a <see cref="ProductReviewResponse"/> using the compiled projection.</summary>
    public static ProductReviewResponse ToResponse(this ProductReviewEntity review) =>
        CompiledProjection(review);
}
