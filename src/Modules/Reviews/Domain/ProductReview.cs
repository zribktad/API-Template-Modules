namespace Reviews.Domain;

/// <summary>
///     Domain entity representing a user's review of a product, including a 1–5 star rating and an optional comment.
/// </summary>
public sealed class ProductReview : IAuditableTenantEntity, IHasId
{
    public Guid ProductId { get; set; }
    public Guid UserId { get; set; }
    public string? Comment { get; set; }

    /// <summary>Rating value object enforcing a 1–5 range via <see cref="Rating.Create" />.</summary>
    public Rating Rating { get; set; }

    public Guid TenantId { get; set; }
    public AuditInfo Audit { get; set; } = new();
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedBy { get; set; }
    public Guid Id { get; set; }

    public static ProductReview Create(Guid productId, Guid userId, Rating rating, string? comment)
    {
        return new ProductReview
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            UserId = userId,
            Rating = rating,
            Comment = comment,
        };
    }
}
