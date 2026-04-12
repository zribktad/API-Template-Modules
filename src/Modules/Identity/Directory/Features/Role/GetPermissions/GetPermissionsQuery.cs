using ErrorOr;
using Identity.Auth.Security;
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
        var isPlatformAdmin =
            user?.HasClaim(AuthConstants.Claims.Permission, Permission.Platform.Manage) == true;

        var allPermissions = Permission.All.ToList();

        if (!isPlatformAdmin)
            allPermissions.Remove(Permission.Platform.Manage);

        return Task.FromResult<ErrorOr<IReadOnlyList<string>>>(allPermissions);
    }
}
