using System.Linq.Expressions;
using Identity.Application.Features.User.DTOs;
using Identity.Domain.Entities;

namespace Identity.Application.Features.User.Mappings;

/// <summary>
/// Provides LINQ-compatible projection expressions and in-process mapping helpers for <see cref="AppUser"/> entities.
/// </summary>
public static class UserMappings
{
    /// <summary>
    /// Expression tree used by EF Core to project an <see cref="AppUser"/> entity directly to a <see cref="UserResponse"/> in the database query.
    /// </summary>
    public static readonly Expression<Func<AppUser, UserResponse>> Projection =
        u => new UserResponse(u.Id, u.Username, u.Email, u.IsActive, u.Role, u.Audit.CreatedAtUtc);

    private static readonly Func<AppUser, UserResponse> CompiledProjection = Projection.Compile();

    /// <summary>
    /// Maps an <see cref="AppUser"/> entity to a <see cref="UserResponse"/> using the pre-compiled projection.
    /// </summary>
    public static UserResponse ToResponse(this AppUser user) => CompiledProjection(user);
}
