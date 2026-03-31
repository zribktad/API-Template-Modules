namespace APITemplate.Domain.Entities;

/// <summary>
/// Domain entity representing a product category within a tenant.
/// Acts as an aggregate root that groups related <see cref="Product"/> entities.
/// </summary>
public sealed class Category : IAuditableTenantEntity, IHasId
{
    public Guid Id { get; set; }

    public required string Name
    {
        get => field;
        set =>
            field = string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("Category name cannot be empty.", nameof(Name))
                : value.Trim();
    }

    public string? Description { get; set; }

    public ICollection<Product> Products { get; set; } = [];

    public Guid TenantId { get; set; }
    public AuditInfo Audit { get; set; } = new();
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedBy { get; set; }
}
