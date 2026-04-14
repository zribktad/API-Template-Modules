using Identity.Auth.Security;
using Identity.Logging;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Identity.Directory.Features.User.InvalidatePermissions;

public sealed record UserPermissionsInvalidatedEvent(Guid AppUserId, string? KeycloakUserId);

public sealed class UserPermissionsInvalidatedEventHandler
{
    public static async Task HandleAsync(
        UserPermissionsInvalidatedEvent message,
        IDistributedCache cache,
        ILogger<UserPermissionsInvalidatedEventHandler> logger,
        CancellationToken ct
    )
    {
        string appUserKey = AuthConstants.DistributedCache.UserPermissionsCacheKey(
            message.AppUserId.ToString()
        );

        if (!string.IsNullOrEmpty(message.KeycloakUserId))
        {
            string keycloakKey = AuthConstants.DistributedCache.UserPermissionsCacheKey(
                message.KeycloakUserId
            );
            await Task.WhenAll(
                cache.RemoveAsync(keycloakKey, ct),
                cache.RemoveAsync(appUserKey, ct)
            );
        }
        else
        {
            await cache.RemoveAsync(appUserKey, ct);
        }

        logger.PermissionCacheInvalidatedForUser(message.AppUserId);
    }
}
