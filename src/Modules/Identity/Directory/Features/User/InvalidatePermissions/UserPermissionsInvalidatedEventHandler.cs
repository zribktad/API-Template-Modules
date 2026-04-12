using Identity.Auth.Security;
using Identity.Directory.Features.User.AssignRoles;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Identity.Directory.Features.User.InvalidatePermissions;

public sealed class UserPermissionsInvalidatedEventHandler
{
    public static async Task HandleAsync(
        UserPermissionsInvalidatedEvent message,
        IDistributedCache cache,
        ILogger<UserPermissionsInvalidatedEventHandler> logger,
        CancellationToken ct
    )
    {
        if (!string.IsNullOrEmpty(message.KeycloakUserId))
        {
            string cacheKey = AuthConstants.DistributedCache.UserPermissionsCacheKey(
                message.KeycloakUserId
            );
            await cache.RemoveAsync(cacheKey, ct);
            logger.LogInformation(
                "Invalidated permissions cache for KeycloakUserId {KeycloakUserId}",
                message.KeycloakUserId
            );
        }

        string appUserCacheKey = AuthConstants.DistributedCache.UserPermissionsCacheKey(
            message.AppUserId.ToString()
        );
        await cache.RemoveAsync(appUserCacheKey, ct);
        logger.LogInformation(
            "Invalidated permissions cache for AppUserId {AppUserId}",
            message.AppUserId
        );
    }
}
