using System.Security.Claims;
using System.Text.Json;
using Identity.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Auth.Security;

public sealed class UserPermissionsClaimsTransformation : IClaimsTransformation
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDistributedCache _cache;

    public UserPermissionsClaimsTransformation(
        IServiceProvider serviceProvider,
        IDistributedCache cache
    )
    {
        _serviceProvider = serviceProvider;
        _cache = cache;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (
            principal.Identity?.IsAuthenticated != true
            || principal.HasClaim(c => c.Type == AuthConstants.Claims.Permission)
        )
            return principal;

        string? sub = principal.FindFirstValue(AuthConstants.Claims.Subject);
        if (string.IsNullOrEmpty(sub))
            return principal;

        if (KeycloakServiceAccountClaims.IsServiceAccount(principal))
        {
            var identity = new ClaimsIdentity();
            foreach (var roleClaim in principal.FindAll(ClaimTypes.Role))
            {
                if (roleClaim.Value == AuthConstants.Policies.PlatformAdmin)
                    identity.AddClaim(
                        new Claim(AuthConstants.Claims.Permission, Permission.Platform.Manage)
                    );
                else if (roleClaim.Value == AuthConstants.Policies.TenantAdmin)
                    identity.AddClaim(
                        new Claim(AuthConstants.Claims.Permission, Permission.Tenant.Manage)
                    );
            }
            if (identity.Claims.Any())
                principal.AddIdentity(identity);
            return principal;
        }

        string cacheKey = AuthConstants.DistributedCache.UserPermissionsCacheKey(sub);
        string? permissionsJson = await _cache.GetStringAsync(cacheKey);
        List<string>? permissions = null;

        if (!string.IsNullOrEmpty(permissionsJson))
        {
            try
            {
                permissions = JsonSerializer.Deserialize<List<string>>(permissionsJson);
            }
            catch (JsonException)
            {
                await _cache.RemoveAsync(cacheKey);
            }
        }

        if (permissions == null)
        {
            bool isGuid = Guid.TryParse(sub, out Guid subGuid);
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

            permissions = await dbContext
                .Users.AsNoTracking()
                .Where(u => u.KeycloakUserId == sub || (isGuid && u.Id == subGuid))
                .SelectMany(u => u.Roles)
                .SelectMany(r => r.Permissions)
                .Select(rp => rp.Permission)
                .Distinct()
                .ToListAsync();

            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = AuthConstants.DistributedCache.UserPermissionsTtl,
            };
            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(permissions),
                cacheOptions
            );
        }

        if (permissions != null && permissions.Count > 0)
        {
            var identity = new ClaimsIdentity();
            foreach (var perm in permissions)
            {
                identity.AddClaim(new Claim(AuthConstants.Claims.Permission, perm));
            }
            principal.AddIdentity(identity);
        }

        return principal;
    }
}
