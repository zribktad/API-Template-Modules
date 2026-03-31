using APITemplate.Domain.Entities;
using Ardalis.Specification;

namespace APITemplate.Application.Features.User.Specifications;

/// <summary>
/// Ardalis specification that filters users by their pre-normalised username.
/// </summary>
public sealed class UserByUsernameSpecification : Specification<AppUser>
{
    /// <summary>
    /// Initialises the specification to match the user with the given <paramref name="normalizedUsername"/>.
    /// </summary>
    public UserByUsernameSpecification(string normalizedUsername)
    {
        Query.Where(u => u.NormalizedUsername == normalizedUsername);
    }
}
