namespace APITemplate.Domain.Enums;

/// <summary>
/// Defines the authorization role assigned to an <see cref="Entities.AppUser"/>.
/// </summary>
public enum UserRole
{
    /// <summary>A regular user with standard access within their tenant.</summary>
    User = 0,

    /// <summary>A super-administrator with platform-wide access across all tenants.</summary>
    PlatformAdmin = 1,

    /// <summary>An administrator with elevated access scoped to a single tenant.</summary>
    TenantAdmin = 2,
}
