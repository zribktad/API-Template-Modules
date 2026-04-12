using Ardalis.Specification;

namespace Identity.Directory.Features.User;

/// <summary>
///     Ardalis specification that finds a user by Keycloak subject ID, bypassing global query filters
///     (no tenant context during token validation).
/// </summary>
public sealed class UserByKeycloakUserIdSpecification : Specification<AppUser>
{
    public UserByKeycloakUserIdSpecification(string keycloakUserId)
    {
        Query.IgnoreQueryFilters().Where(u => u.KeycloakUserId == keycloakUserId).AsNoTracking();
    }
}
