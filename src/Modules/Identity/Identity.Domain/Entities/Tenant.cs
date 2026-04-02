using Identity.Domain.ValueObjects;

namespace Identity.Domain.Entities;

/// <summary>
/// Aggregate root representing a tenant (organisation) in the multi-tenant system.
/// All other tenant-scoped entities reference this entity through <see cref="TenantId"/>.
/// </summary>
public sealed class Tenant : IAuditableTenantEntity, IHasId
{
    public Guid Id { get; set; }

    public required TenantCode Code { get; set; }

    public required string Name
    {
        get => field;
        set =>
            field = string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("Tenant name cannot be empty.", nameof(Name))
                : value.Trim();
    }

    public bool IsActive { get; private set; } = true;

    public Guid TenantId { get; set; }
    public AuditInfo Audit { get; set; } = new();
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedBy { get; set; }

    public static Tenant Create(Guid id, string code, string name) =>
        new()
        {
            Id = id,
            TenantId = id,
            Code = TenantCode.FromPersistence(code.Trim()),
            Name = name,
        };

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
