using Ardalis.Specification;

namespace Identity.Directory.Features.User;

/// <summary>
///     Ardalis specification that filters users by their pre-normalised username.
/// </summary>
public sealed class UserByUsernameSpecification : Specification<AppUser>
{
    public UserByUsernameSpecification(string normalizedUsername)
    {
        Query.Where(u => u.Username.Normalized == normalizedUsername).AsNoTracking();
    }
}
