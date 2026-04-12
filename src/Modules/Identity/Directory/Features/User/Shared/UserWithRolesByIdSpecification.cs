using Ardalis.Specification;
using Identity.Directory.Entities;
using SharedKernel.Domain.Interfaces;

namespace Identity.Directory.Features.User.Shared;

public sealed class UserWithRolesByIdSpecification : SingleResultSpecification<AppUser>
{
    public UserWithRolesByIdSpecification(Guid id)
    {
        Query.Where(u => u.Id == id).Include(u => u.Roles);
    }
}
