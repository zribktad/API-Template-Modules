using ErrorOr;
using Identity.ValueObjects;
using SharedKernel.Contracts.Security;
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

    public static ErrorOr<CustomRole> Create(
        Guid id,
        string name,
        Guid? tenantId,
        IEnumerable<string> permissions,
        bool isPlatformAdmin
    )
    {
        List<string> permList = permissions.ToList();

        if (!isPlatformAdmin && permList.Contains(Permission.Platform.Manage))
            return DomainErrors.Roles.CannotGrantPlatformManage();

        CustomRole role = new()
        {
            Id = id,
            Name = name,
            TenantId = tenantId,
            IsImmutable = false,
        };
        role.SetPermissions(permList);
        return role;
    }

    public void SetPermissions(IEnumerable<string> permissions)
    {
        Permissions.Clear();
        foreach (string perm in permissions)
            Permissions.Add(new RolePermission { RoleId = Id, Permission = perm });
    }
}
