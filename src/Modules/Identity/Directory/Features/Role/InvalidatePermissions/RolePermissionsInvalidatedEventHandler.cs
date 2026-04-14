using Ardalis.Specification;
using Identity.Logging;
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

        List<Task> removals = new(users.Count * 2);
        foreach (var user in users)
        {
            if (!string.IsNullOrEmpty(user.KeycloakUserId))
                removals.Add(
                    cache.RemoveAsync(
                        AuthConstants.DistributedCache.UserPermissionsCacheKey(user.KeycloakUserId),
                        ct
                    )
                );

            removals.Add(
                cache.RemoveAsync(
                    AuthConstants.DistributedCache.UserPermissionsCacheKey(user.Id.ToString()),
                    ct
                )
            );
        }
        await Task.WhenAll(removals);

        logger.PermissionCacheInvalidatedForRole(users.Count, message.RoleId);
    }
}
