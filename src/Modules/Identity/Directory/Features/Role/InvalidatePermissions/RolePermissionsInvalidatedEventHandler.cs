using Ardalis.Specification;
using Identity.Auth.Security;
using Identity.Directory.Entities;
using Identity.Directory.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Identity.Directory.Features.Role.InvalidatePermissions;

public sealed record RolePermissionsInvalidatedEvent(Guid RoleId);

/// <summary>Projection of user identifiers used only for permission cache key eviction.</summary>
public readonly record struct UserPermissionCacheKeySource(Guid Id, string? KeycloakUserId);

public sealed class UsersByRoleIdForCacheInvalidationSpecification
    : Specification<AppUser, UserPermissionCacheKeySource>
{
    public UsersByRoleIdForCacheInvalidationSpecification(Guid roleId)
    {
        Query.Where(u => u.Roles.Any(r => r.Id == roleId));
        Query.AsNoTracking();
        Query.Select(u => new UserPermissionCacheKeySource(u.Id, u.KeycloakUserId));
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
            new UsersByRoleIdForCacheInvalidationSpecification(message.RoleId),
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
