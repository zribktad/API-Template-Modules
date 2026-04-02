using APITemplate.Domain.Enums;

namespace APITemplate.Application.Common.Security;

/// <summary>
/// Compile-time implementation of <see cref="IRolePermissionMap"/> that maps each
/// <see cref="UserRole"/> to a fixed set of permission strings.
/// The mapping is built once and cached for the lifetime of the application.
/// </summary>
public sealed class StaticRolePermissionMap : IRolePermissionMap
{
    private static readonly IReadOnlySet<string> Empty = new HashSet<string>(
        StringComparer.Ordinal
    );

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Map = BuildMap();

    public IReadOnlySet<string> GetPermissions(string role) =>
        Map.TryGetValue(role, out var permissions) ? permissions : Empty;

    public bool HasPermission(string role, string permission) =>
        GetPermissions(role).Contains(permission);

    /// <summary>
    /// Constructs the static role-to-permissions dictionary used for all permission lookups.
    /// </summary>
    private static Dictionary<string, IReadOnlySet<string>> BuildMap()
    {
        var tenantAdminPermissions = new HashSet<string>(StringComparer.Ordinal)
        {
            Permission.Products.Read,
            Permission.Products.Create,
            Permission.Products.Update,
            Permission.Products.Delete,
            Permission.Categories.Read,
            Permission.Categories.Create,
            Permission.Categories.Update,
            Permission.Categories.Delete,
            Permission.ProductReviews.Read,
            Permission.ProductReviews.Create,
            Permission.ProductReviews.Delete,
            Permission.ProductData.Read,
            Permission.ProductData.Create,
            Permission.ProductData.Delete,
            Permission.Users.Read,
            Permission.Invitations.Read,
            Permission.Invitations.Create,
            Permission.Invitations.Revoke,
            Permission.Examples.Read,
            Permission.Examples.Create,
            Permission.Examples.Update,
            Permission.Examples.Execute,
            Permission.Examples.Upload,
            Permission.Examples.Download,
        };

        var userPermissions = new HashSet<string>(StringComparer.Ordinal)
        {
            Permission.Products.Read,
            Permission.Categories.Read,
            Permission.ProductReviews.Read,
            Permission.ProductReviews.Create,
            Permission.ProductData.Read,
            Permission.Examples.Read,
            Permission.Examples.Download,
        };

        return new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [UserRole.PlatformAdmin.ToString()] = Permission.All,
            [UserRole.TenantAdmin.ToString()] = tenantAdminPermissions,
            [UserRole.User.ToString()] = userPermissions,
        };
    }
}
