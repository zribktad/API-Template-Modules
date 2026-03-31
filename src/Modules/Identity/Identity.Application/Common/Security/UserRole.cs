namespace Identity.Application.Common.Security;

/// <summary>
/// Defines the authorization role assigned to an authenticated principal.
/// </summary>
public enum UserRole
{
    User = 0,
    PlatformAdmin = 1,
    TenantAdmin = 2,
}
