using System.Linq.Expressions;
using Identity.Directory.Entities;

namespace Identity.Directory.Features.Role.Shared;

public static class RoleMappings
{
    /// <summary>
    ///     Expression tree used by EF Core to project a <see cref="CustomRole" /> entity directly to a
    ///     <see cref="RoleResponse" /> in the database query.
    /// </summary>
    public static readonly Expression<Func<CustomRole, RoleResponse>> Projection =
        role => new RoleResponse(
            role.Id,
            role.Name,
            role.IsImmutable,
            role.Permissions.Select(p => p.Permission).ToList()
        );

    public static RoleResponse ToResponse(this CustomRole role) =>
        new(
            role.Id,
            role.Name,
            role.IsImmutable,
            role.Permissions.Select(p => p.Permission).ToList()
        );
}
