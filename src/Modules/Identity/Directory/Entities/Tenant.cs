namespace Identity.Directory.Entities;

/// <summary>
///     Aggregate root representing a tenant (organisation) in the multi-tenant system.
///     All other tenant-scoped entities reference this entity through <see cref="TenantId" />.
/// </summary>
public sealed class Tenant : IAuditableTenantEntity, IHasId
{
    public const int CodeMaxLength = 100;
    public const int NameMaxLength = 200;

    public required string Code { get; init; }

    public required string Name
    {
        get => field;
        set =>
            field = string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("Tenant name cannot be empty.", nameof(Name))
                : value.Trim();
    }

    public bool IsActive { get; set; } = true;

    public Guid TenantId { get; set; }
    public AuditInfo Audit { get; set; } = new();
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedBy { get; set; }
    public Guid Id { get; set; }

    public static Tenant Create(Guid id, string code, string name)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Tenant code cannot be empty.", nameof(code));

        return new Tenant
        {
            Id = id,
            TenantId = id,
            Code = code.Trim(),
            Name = name,
        };
    }
}
