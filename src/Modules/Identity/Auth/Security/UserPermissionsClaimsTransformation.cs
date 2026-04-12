using System.Security.Claims;
using System.Text.Json;
using Identity.Auth.Security;
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
            || principal.HasClaim(c => c.Type == "Permission")
        )
            return principal;

        // Extract subject (Keycloak user ID or Local AppUser ID)
        string? sub = principal.FindFirstValue(AuthConstants.Claims.Subject);
        if (string.IsNullOrEmpty(sub))
            return principal;

        // If it's a Keycloak service account, it might have Realm roles mapped to ClaimTypes.Role.
        // We could optionally map those to Permission claims here if needed, but for now we focus on human users.
        if (KeycloakServiceAccountClaims.IsServiceAccount(principal))
        {
            var identity = new ClaimsIdentity();
            foreach (var roleClaim in principal.FindAll(ClaimTypes.Role))
            {
                // Simple mapping: if service account has PlatformAdmin role, grant Platform.Manage permission
                if (roleClaim.Value == "PlatformAdmin")
                    identity.AddClaim(
                        new Claim(
                            "Permission",
                            SharedKernel.Contracts.Security.Permission.Platform.Manage
                        )
                    );
                else if (roleClaim.Value == "TenantAdmin")
                    identity.AddClaim(
                        new Claim(
                            "Permission",
                            SharedKernel.Contracts.Security.Permission.Tenant.Manage
                        )
                    );
            }
            if (identity.Claims.Any())
                principal.AddIdentity(identity);
            return principal;
        }

        string cacheKey = $"UserPermissions:{sub}";
        string? permissionsJson = await _cache.GetStringAsync(cacheKey);
        List<string>? permissions = null;

        if (!string.IsNullOrEmpty(permissionsJson))
        {
            permissions = JsonSerializer.Deserialize<List<string>>(permissionsJson);
        }
        else
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

            permissions = await dbContext
                .Users.AsNoTracking()
                .Where(u => u.KeycloakUserId == sub || u.Id.ToString() == sub)
                .SelectMany(u => u.Roles)
                .SelectMany(r => r.Permissions)
                .Select(rp => rp.Permission)
                .Distinct()
                .ToListAsync();

            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
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
                identity.AddClaim(new Claim("Permission", perm));
            }
            principal.AddIdentity(identity);
        }

        return principal;
    }
}
