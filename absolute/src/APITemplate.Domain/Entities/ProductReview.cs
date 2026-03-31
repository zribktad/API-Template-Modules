namespace APITemplate.Domain.Entities;

/// <summary>
/// Domain entity representing a user's review of a product, including a 1–5 star rating and an optional comment.
/// </summary>
public sealed class ProductReview : IAuditableTenantEntity, IHasId
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid UserId { get; set; }
    public string? Comment { get; set; }

    /// <summary>Integer score from 1 (worst) to 5 (best); throws <see cref="ArgumentOutOfRangeException"/> on assignment outside that range.</summary>
    public int Rating
    {
        get => field;
        set =>
            field = value is >= 1 and <= 5
                ? value
                : throw new ArgumentOutOfRangeException(
                    nameof(Rating),
                    "Rating must be between 1 and 5."
                );
    }

    public Product Product { get; set; } = null!;
    public AppUser User { get; set; } = null!;

    public Guid TenantId { get; set; }
    public AuditInfo Audit { get; set; } = new();
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedBy { get; set; }
}
