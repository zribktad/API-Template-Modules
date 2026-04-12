using Ardalis.Specification;

namespace Identity.Directory.Features.User;

/// <summary>
///     Loads a single user by Keycloak user id and projects to
///     <see cref="UserResponse" />.
/// </summary>
public sealed class UserByKeycloakUserIdAsUserResponseSpecification
    : Specification<AppUser, UserResponse>
{
    public UserByKeycloakUserIdAsUserResponseSpecification(string keycloakUserId)
    {
        Query
            .Where(u => u.KeycloakUserId == keycloakUserId)
            .AsNoTracking()
            .Select(UserMappings.Projection);
    }
}
