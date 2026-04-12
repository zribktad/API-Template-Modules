using System.Security.Claims;
using Identity.Auth.Security;
using Microsoft.AspNetCore.Authorization;

namespace APITemplate.Api.Authorization;

public sealed class PermissionAuthorizationHandler(IRolePermissionMap rolePermissionMap)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement
    )
    {
        IEnumerable<Claim> roleClaims = context.User.FindAll(ClaimTypes.Role);

        foreach (Claim roleClaim in roleClaims)
        {
            if (rolePermissionMap.HasPermission(roleClaim.Value, requirement.Permission))
            {
                context.Succeed(requirement);
                break;
            }
        }

        return Task.CompletedTask;
    }
}
