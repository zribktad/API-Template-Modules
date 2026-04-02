namespace APITemplate.Domain.Entities;

/// <summary>
/// Join entity that associates a <see cref="Product"/> with a <see cref="ProductData.ProductData"/> document stored in MongoDB.
/// Supports soft-delete so that links can be restored without data loss.
/// </summary>
public sealed class ProductDataLink : IAuditableTenantEntity
{
    public Guid ProductId { get; set; }

    public Guid ProductDataId { get; set; }

    public Guid TenantId { get; set; }

    public AuditInfo Audit { get; set; } = new();

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public Guid? DeletedBy { get; set; }

    public Product Product { get; set; } = null!;

    /// <summary>
    /// Factory method that creates a new <see cref="ProductDataLink"/> for the given product and product-data pair.
    /// </summary>
    public static ProductDataLink Create(Guid productId, Guid productDataId) =>
        new() { ProductId = productId, ProductDataId = productDataId };

    /// <summary>
    /// Clears all soft-delete fields, effectively un-deleting this link.
    /// </summary>
    public void Restore()
    {
        IsDeleted = false;
        DeletedAtUtc = null;
        DeletedBy = null;
    }
}
