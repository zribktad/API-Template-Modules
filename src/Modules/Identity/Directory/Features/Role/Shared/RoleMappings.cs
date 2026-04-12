using System.Linq.Expressions;
using Identity.Directory.Entities;

namespace Identity.Directory.Features.Role.Shared;

/// <summary>
///     Provides LINQ-compatible projection expressions and in-process mapping helpers for <see cref="CustomRole" /> entities.
/// </summary>
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

    private static readonly Func<CustomRole, RoleResponse> CompiledProjection = Projection.Compile();

    /// <summary>
    ///     Maps a <see cref="CustomRole" /> entity to a <see cref="RoleResponse" /> using the pre-compiled projection.
    /// </summary>
    public static RoleResponse ToResponse(this CustomRole role)
    {
        return CompiledProjection(role);
    }
}
