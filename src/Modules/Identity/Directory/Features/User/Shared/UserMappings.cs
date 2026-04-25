using System.Linq.Expressions;

namespace Identity.Directory.Features.User;

public static class UserMappings
{
    // Expression tree — EF Core translates this to SQL; the compiled delegate is reused for in-memory mapping.
    public static readonly Expression<Func<AppUser, UserResponse>> Projection =
        u => new UserResponse(
            u.Id,
            u.DbUsername,
            u.DbEmail,
            u.IsActive,
            u.Roles.Select(r => r.Name).ToList(),
            u.ProvisioningStatus,
            u.Audit.CreatedAtUtc
        );

    private static readonly Func<AppUser, UserResponse> CompiledProjection = Projection.Compile();

    public static UserResponse ToResponse(this AppUser user)
    {
        return CompiledProjection(user);
    }
}
