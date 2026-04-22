using Ardalis.Specification;

namespace Identity.Directory.Features.User;

/// <summary>
///     Matches users by username. Expects a pre-normalised value (see <see cref="NormalizedString.Normalize"/>);
///     passing a raw username will produce case-sensitive results and miss records.
/// </summary>
public sealed class UserByUsernameSpecification : Specification<AppUser>
{
    public UserByUsernameSpecification(string normalizedUsername)
    {
        Query.Where(u => u.Username.Normalized == normalizedUsername).AsNoTracking();
    }
}
