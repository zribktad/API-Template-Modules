using Ardalis.Specification;
using Identity.Auth.Security;
using Identity.Directory.Entities;
using Identity.Directory.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Identity.Directory.Features.User.InvalidatePermissions;

public sealed record RolePermissionsInvalidatedEvent(Guid RoleId);

public sealed class UsersByRoleIdSpecification : Specification<AppUser>
{
    public UsersByRoleIdSpecification(Guid roleId)
    {
        Query.Where(u => u.Roles.Any(r => r.Id == roleId));
    }
}

public sealed class RolePermissionsInvalidatedEventHandler
{
    public static async Task HandleAsync(
        RolePermissionsInvalidatedEvent message,
        IUserRepository userRepository,
        IDistributedCache cache,
        ILogger<RolePermissionsInvalidatedEventHandler> logger,
        CancellationToken ct
    )
    {
        var users = await userRepository.ListAsync(
            new UsersByRoleIdSpecification(message.RoleId),
            ct
        );

        foreach (var user in users)
        {
            if (!string.IsNullOrEmpty(user.KeycloakUserId))
            {
                string cacheKey = AuthConstants.DistributedCache.UserPermissionsCacheKey(
                    user.KeycloakUserId
                );
                await cache.RemoveAsync(cacheKey, ct);
            }

            string appUserCacheKey = AuthConstants.DistributedCache.UserPermissionsCacheKey(
                user.Id.ToString()
            );
            await cache.RemoveAsync(appUserCacheKey, ct);
        }

        logger.LogInformation(
            "Invalidated permissions cache for {Count} users assigned to RoleId {RoleId}",
            users.Count,
            message.RoleId
        );
    }
}
