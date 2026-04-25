using Ardalis.Specification;

namespace Identity.Directory.Features.User;

/// <summary>
///     Internal extension that applies shared <see cref="UserFilter" /> criteria to an Ardalis specification builder.
/// </summary>
internal static class UserFilterCriteria
{
    /// <summary>
    ///     Adds optional normalised-username contains, email exact-match, active-status, and role predicates to the query.
    /// </summary>
    internal static void ApplyFilter(this ISpecificationBuilder<AppUser> query, UserFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Username))
        {
            string normalizedUsername = NormalizedString.Normalize(filter.Username);
            query.Where(u => u.DbNormalizedUsername.Contains(normalizedUsername));
        }

        if (!string.IsNullOrWhiteSpace(filter.Email))
        {
            string normalizedEmail = NormalizedString.Normalize(filter.Email);
            query.Where(u => u.DbNormalizedEmail == normalizedEmail);
        }

        if (filter.IsActive.HasValue)
            query.Where(u => u.IsActive == filter.IsActive.Value);

        if (filter.RoleId.HasValue)
            query.Where(u => u.Roles.Any(r => r.Id == filter.RoleId.Value));

        if (filter.ProvisioningStatus.HasValue)
            query.Where(u => u.ProvisioningStatus == filter.ProvisioningStatus.Value);
    }
}
