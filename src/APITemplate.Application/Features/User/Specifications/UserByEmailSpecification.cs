using APITemplate.Domain.Entities;
using Ardalis.Specification;

namespace APITemplate.Application.Features.User.Specifications;

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
        var normalizedEmail = email.Trim().ToUpperInvariant();
        Query.Where(u => u.Email.ToUpper() == normalizedEmail);
    }
}
