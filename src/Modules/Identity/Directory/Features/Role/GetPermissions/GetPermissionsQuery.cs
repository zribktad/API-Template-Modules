using ErrorOr;
using Microsoft.AspNetCore.Http;

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
        bool isPlatformAdmin = httpContextAccessor.HttpContext?.User.IsPlatformAdmin() == true;

        List<string> permissions = isPlatformAdmin
            ? Permission.All.ToList()
            : Permission.All.Where(p => p != Permission.Platform.Manage).ToList();

        return Task.FromResult<ErrorOr<IReadOnlyList<string>>>(permissions);
    }
}
