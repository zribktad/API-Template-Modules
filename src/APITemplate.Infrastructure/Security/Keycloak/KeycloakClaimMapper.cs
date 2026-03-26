using System.Security.Claims;
using System.Text.Json;
using APITemplate.Application.Common.Security;

namespace APITemplate.Infrastructure.Security.Keycloak;

/// <summary>
/// Maps Keycloak-specific JWT claims into standard .NET claim types expected by
/// ASP.NET Core authorization policies (preferred_username → <see cref="ClaimTypes.Name"/>,
/// realm_access.roles → <see cref="ClaimTypes.Role"/>).
/// </summary>
public static class KeycloakClaimMapper
{
    /// <summary>Maps both the preferred username and realm roles from the Keycloak token into standard .NET claims.</summary>
    public static void MapKeycloakClaims(ClaimsIdentity identity)
    {
        MapUsername(identity);
        MapRealmRoles(identity);
    }

    private static void MapUsername(ClaimsIdentity identity)
    {
        if (identity.FindFirst(ClaimTypes.Name) != null)
            return;

        var preferred = identity.FindFirst(AuthConstants.Claims.PreferredUsername);
        if (preferred != null)
            identity.AddClaim(new Claim(ClaimTypes.Name, preferred.Value));
    }

    private static void MapRealmRoles(ClaimsIdentity identity)
    {
        var realmAccess = identity.FindFirst(AuthConstants.Claims.RealmAccess);
        if (realmAccess == null)
            return;

        using var doc = JsonDocument.Parse(realmAccess.Value);
        if (!doc.RootElement.TryGetProperty(AuthConstants.Claims.Roles, out var roles))
            return;

        foreach (var role in roles.EnumerateArray())
        {
            var value = role.GetString();
            if (!string.IsNullOrEmpty(value))
                identity.AddClaim(new Claim(ClaimTypes.Role, value));
        }
    }
}
