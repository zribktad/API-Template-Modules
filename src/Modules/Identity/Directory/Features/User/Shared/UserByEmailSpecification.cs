using Ardalis.Specification;

namespace Identity.Directory.Features.User;

/// <summary>
///     Matches users by email, case-insensitively. Pass the raw email — normalisation is applied internally.
/// </summary>
public sealed class UserByEmailSpecification : Specification<AppUser>
{
    public UserByEmailSpecification(string email)
    {
        string normalizedEmail = NormalizedString.Normalize(email);
        Query.Where(u => u.Email.Normalized == normalizedEmail).AsNoTracking();
    }
}
