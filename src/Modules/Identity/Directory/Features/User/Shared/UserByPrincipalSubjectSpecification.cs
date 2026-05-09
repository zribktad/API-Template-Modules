using Ardalis.Specification;
using Identity.Directory.Entities;

namespace Identity.Directory.Features.User;

/// <summary>
///     Finds a user by Keycloak subject string or by
///     application user id. Ignores global query filters (no tenant context during claims transformation).
/// </summary>
public sealed class UserByPrincipalSubjectSpecification : Specification<AppUser>
{
    public UserByPrincipalSubjectSpecification(
        string subject,
        bool subjectMatchesApplicationUserId,
        Guid subjectAsApplicationUserId
    )
    {
        Query
            .IgnoreQueryFilters()
            .Where(u =>
                u.KeycloakUserId == subject
                || (subjectMatchesApplicationUserId && u.Id == subjectAsApplicationUserId)
            )
            .AsNoTracking();
    }
}
