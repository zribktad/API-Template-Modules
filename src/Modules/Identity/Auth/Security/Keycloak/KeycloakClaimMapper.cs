using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Identity.Auth.Security.Keycloak;

/// <summary>
///     Maps Keycloak-specific JWT claims into standard .NET claim types expected by
///     ASP.NET Core authorization policies (preferred_username → <see cref="ClaimTypes.Name" />,
///     realm_access.roles → <see cref="ClaimTypes.Role" />).
/// </summary>
public static class KeycloakClaimMapper
{
    /// <summary>Maps both the preferred username and realm roles from the Keycloak token into standard .NET claims.</summary>
    public static void MapKeycloakClaims(ClaimsIdentity identity, ILogger? logger = null)
    {
        MapUsername(identity);
        MapRealmRoles(identity, logger);
    }

    private static void MapUsername(ClaimsIdentity identity)
    {
        if (identity.FindFirst(ClaimTypes.Name) != null)
            return;

        Claim? preferred = identity.FindFirst(AuthConstants.Claims.PreferredUsername);
        if (preferred != null)
            identity.AddClaim(new Claim(ClaimTypes.Name, preferred.Value));
    }

    private static void MapRealmRoles(ClaimsIdentity identity, ILogger? logger)
    {
        Claim? realmAccess = identity.FindFirst(AuthConstants.Claims.RealmAccess);
        if (realmAccess == null)
            return;

        try
        {
            using JsonDocument doc = JsonDocument.Parse(realmAccess.Value);
            if (
                !doc.RootElement.TryGetProperty(AuthConstants.Claims.Roles, out JsonElement roles)
                || roles.ValueKind != JsonValueKind.Array
            )
                return;

            foreach (JsonElement role in roles.EnumerateArray())
            {
                string? value = role.GetString();
                if (!string.IsNullOrEmpty(value))
                    identity.AddClaim(new Claim(ClaimTypes.Role, value));
            }
        }
        catch (JsonException ex)
        {
            logger?.LogWarning(
                ex,
                "Failed to parse Keycloak 'realm_access' claim as JSON. Skipping role mapping. Raw value: {RawValue}",
                realmAccess.Value
            );
        }
    }
}
