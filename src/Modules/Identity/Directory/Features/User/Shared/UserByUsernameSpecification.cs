using Ardalis.Specification;

namespace Identity.Directory.Features.User;

/// <summary>
///     Matches users by username, case-insensitively. Pass the raw username — normalisation is applied internally.
/// </summary>
public sealed class UserByUsernameSpecification : Specification<AppUser>
{
    public UserByUsernameSpecification(string username)
    {
        string normalizedUsername = NormalizedString.Normalize(username);
        Query.Where(u => u.DbNormalizedUsername == normalizedUsername).AsNoTracking();
    }
}
