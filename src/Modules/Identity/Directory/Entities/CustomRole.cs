using Identity.ValueObjects;
using SharedKernel.Domain.Interfaces;

namespace Identity.Directory.Entities;

public sealed class CustomRole : IAuditableEntity, IHasId, ISoftDeletable
{
    public Guid Id { get; set; }

    /// <summary>
    /// If null, this is a global role (e.g., PlatformAdmin, TenantAdmin, User).
    /// If set, this is a tenant-specific custom role.
    /// </summary>
    public Guid? TenantId { get; set; }

    public required string Name { get; set; }

    /// <summary>
    /// Determines whether the role can be modified or deleted.
    /// Built-in roles (PlatformAdmin, TenantAdmin, User) are immutable.
    /// </summary>
    public bool IsImmutable { get; set; }

    public AuditInfo Audit { get; set; } = new();

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedBy { get; set; }

    public ICollection<AppUser> Users { get; set; } = new List<AppUser>();
    public ICollection<RolePermission> Permissions { get; set; } = new List<RolePermission>();
}
