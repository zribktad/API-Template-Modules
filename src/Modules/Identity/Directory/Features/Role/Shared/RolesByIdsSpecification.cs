using Ardalis.Specification;
using Identity.Directory.Entities;

namespace Identity.Directory.Features.Role.Shared;

public sealed class RolesByIdsSpecification : Specification<CustomRole>
{
    public RolesByIdsSpecification(IEnumerable<Guid> ids)
    {
        Query.Where(r => ids.Contains(r.Id));
    }
}