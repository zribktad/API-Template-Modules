using Ardalis.Specification;

namespace Identity.Directory.Features.User;

/// <summary>
///     Ardalis specification that filters users by a case-insensitive exact email match.
/// </summary>
public sealed class UserByEmailSpecification : Specification<AppUser>
{
    public UserByEmailSpecification(string email)
    {
        string normalizedEmail = NormalizedString.Normalize(email);
        Query.Where(u => u.Email.Normalized == normalizedEmail).AsNoTracking();
    }
}
