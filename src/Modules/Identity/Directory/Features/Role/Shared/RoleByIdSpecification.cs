using Ardalis.Specification;
using Identity.Directory.Entities;

namespace Identity.Directory.Features.Role.Shared;

public sealed class RoleByIdSpecification : SingleResultSpecification<CustomRole>
{
    public RoleByIdSpecification(Guid id)
    {
        Query.Where(r => r.Id == id).Include(r => r.Permissions);
    }
}