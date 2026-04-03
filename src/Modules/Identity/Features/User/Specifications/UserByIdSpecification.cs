using Ardalis.Specification;
using Identity.Features.User.DTOs;
using Identity.Features.User.Mappings;
using Identity.Entities;

namespace Identity.Features.User.Specifications;

/// <summary>
/// Ardalis specification that fetches a single user by ID and projects it to <see cref="UserResponse"/>.
/// </summary>
public sealed class UserByIdSpecification : Specification<AppUser, UserResponse>
{
    /// <summary>
    /// Initialises the specification to match the user with the given <paramref name="id"/> and apply the response projection.
    /// </summary>
    public UserByIdSpecification(Guid id)
    {
        Query.Where(u => u.Id == id).AsNoTracking().Select(UserMappings.Projection);
    }
}

