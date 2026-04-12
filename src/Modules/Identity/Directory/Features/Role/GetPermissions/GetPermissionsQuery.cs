using ErrorOr;
using Microsoft.AspNetCore.Http;
using SharedKernel.Contracts.Security;

namespace Identity.Directory.Features.Role.GetPermissions;

public sealed record GetPermissionsQuery();

public sealed class GetPermissionsQueryHandler
{
    public static Task<ErrorOr<IReadOnlyList<string>>> HandleAsync(
        GetPermissionsQuery query,
        IHttpContextAccessor httpContextAccessor,
        CancellationToken ct
    )
    {
        var user = httpContextAccessor.HttpContext?.User;
        var isPlatformAdmin = user?.HasClaim("Permission", Permission.Platform.Manage) == true;

        var allPermissions = Permission.All.ToList();

        if (!isPlatformAdmin)
        {
            // Filter out platform-level permissions for tenant admins
            allPermissions.Remove(Permission.Platform.Manage);

            // Optionally, TenantAdmin might not be allowed to assign Tenant.Manage to avoid creating more admins?
            // If they are allowed, they can keep it. We will just remove Platform.Manage.
        }

        return Task.FromResult<ErrorOr<IReadOnlyList<string>>>(allPermissions);
    }
}
