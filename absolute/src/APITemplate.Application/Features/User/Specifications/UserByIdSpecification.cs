using APITemplate.Application.Features.User.DTOs;
using APITemplate.Application.Features.User.Mappings;
using APITemplate.Domain.Entities;
using Ardalis.Specification;

namespace APITemplate.Application.Features.User.Specifications;

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
        Query.Where(u => u.Id == id).Select(UserMappings.Projection);
    }
}
