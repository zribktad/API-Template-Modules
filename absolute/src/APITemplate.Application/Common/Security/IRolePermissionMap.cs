namespace APITemplate.Application.Common.Security;

/// <summary>
/// Defines the contract for querying which permissions are granted to a given role.
/// Decouples authorization policy evaluation from the concrete permission mapping strategy.
/// </summary>
public interface IRolePermissionMap
{
    /// <summary>Returns the complete set of permission strings granted to <paramref name="role"/>.</summary>
    IReadOnlySet<string> GetPermissions(string role);

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="role"/> has been granted
    /// the specified <paramref name="permission"/>.
    /// </summary>
    bool HasPermission(string role, string permission);
}
