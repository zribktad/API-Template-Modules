using Ardalis.Specification;
using Identity.ValueObjects;

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
            string normalizedUsername = AppUser.NormalizeUsername(filter.Username);
            query.Where(u => u.NormalizedUsername.Contains(normalizedUsername));
        }

        if (!string.IsNullOrWhiteSpace(filter.Email))
        {
            string normalizedEmail = Email.NormalizeRaw(filter.Email);
            query.Where(u => u.NormalizedEmail == normalizedEmail);
        }

        if (filter.IsActive.HasValue)
            query.Where(u => u.IsActive == filter.IsActive.Value);

        if (filter.RoleId.HasValue)
            query.Where(u => u.Roles.Any(r => r.Id == filter.RoleId.Value));

        if (filter.ProvisioningStatus.HasValue)
            query.Where(u => u.ProvisioningStatus == filter.ProvisioningStatus.Value);
    }
}
