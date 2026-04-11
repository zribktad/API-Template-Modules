using Ardalis.Specification;

namespace Identity.Features.User;

/// <summary>
///     Finds a user by normalised email with no Keycloak link yet (admin-created account before
///     linkage), bypassing global query filters for token validation.
/// </summary>
public sealed class UserUnlinkedByNormalizedEmailSpecification : Specification<AppUser>
{
    public UserUnlinkedByNormalizedEmailSpecification(string normalizedEmail)
    {
        Query
            .IgnoreQueryFilters()
            .Where(u => u.NormalizedEmail == normalizedEmail && u.KeycloakUserId == null);
    }
}
