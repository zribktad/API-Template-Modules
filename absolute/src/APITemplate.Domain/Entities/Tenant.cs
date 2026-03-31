namespace APITemplate.Domain.Entities;

/// <summary>
/// Aggregate root representing a tenant (organisation) in the multi-tenant system.
/// All other tenant-scoped entities reference this entity through <see cref="TenantId"/>.
/// </summary>
public sealed class Tenant : IAuditableTenantEntity, IHasId
{
    public Guid Id { get; set; }

    public required string Code
    {
        get => field;
        set =>
            field = string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("Tenant code cannot be empty.", nameof(Code))
                : value.Trim();
    }

    public required string Name
    {
        get => field;
        set =>
            field = string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("Tenant name cannot be empty.", nameof(Name))
                : value.Trim();
    }

    public bool IsActive { get; set; } = true;

    public ICollection<AppUser> Users { get; set; } = [];

    public Guid TenantId { get; set; }
    public AuditInfo Audit { get; set; } = new();
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedBy { get; set; }
}
