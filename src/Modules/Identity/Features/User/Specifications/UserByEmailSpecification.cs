using Ardalis.Specification;
using Identity.Entities;
using Identity.ValueObjects;

namespace Identity.Features.User.Specifications;

/// <summary>
/// Ardalis specification that filters users by a case-insensitive exact email match.
/// </summary>
public sealed class UserByEmailSpecification : Specification<AppUser>
{
    /// <summary>
    /// Initialises the specification to match users whose normalised email equals the normalised form of <paramref name="email"/>.
    /// </summary>
    public UserByEmailSpecification(string email)
    {
        string normalizedEmail = Email.NormalizeRaw(email);
        Query.Where(u => u.NormalizedEmail == normalizedEmail).AsNoTracking();
    }
}

