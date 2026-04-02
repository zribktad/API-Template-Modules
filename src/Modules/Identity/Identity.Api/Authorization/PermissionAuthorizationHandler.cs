using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Identity.Api.Authorization;

/// <summary>
/// ASP.NET Core authorization handler that evaluates a <see cref="PermissionRequirement"/>
/// by checking the current user's role claims against the application's role-permission map.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IRolePermissionMap _rolePermissionMap;

    public PermissionAuthorizationHandler(IRolePermissionMap rolePermissionMap)
    {
        _rolePermissionMap = rolePermissionMap;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement
    )
    {
        IEnumerable<Claim> roleClaims = context.User.FindAll(ClaimTypes.Role);

        foreach (Claim roleClaim in roleClaims)
        {
            if (_rolePermissionMap.HasPermission(roleClaim.Value, requirement.Permission))
            {
                context.Succeed(requirement);
                break;
            }
        }

        return Task.CompletedTask;
    }
}
