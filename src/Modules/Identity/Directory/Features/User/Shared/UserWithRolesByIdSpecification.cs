using Ardalis.Specification;
using BuildingBlocks.Domain.Interfaces;
using Identity.Directory.Entities;

namespace Identity.Directory.Features.User.Shared;

public sealed class UserWithRolesByIdSpecification : SingleResultSpecification<AppUser>
{
    public UserWithRolesByIdSpecification(Guid id)
    {
        Query.Where(u => u.Id == id).Include(u => u.Roles);
    }
}
