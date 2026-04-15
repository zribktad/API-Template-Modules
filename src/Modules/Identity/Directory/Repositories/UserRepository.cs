using Identity.Directory.Features.User;
using Microsoft.EntityFrameworkCore;

namespace Identity.Directory.Repositories;

/// <summary>EF Core repository for <see cref="AppUser" /> with specification-based lookup by email and username.</summary>
public sealed class UserRepository : RepositoryBase<AppUser>, IUserRepository
{
    public UserRepository(IdentityDbContext dbContext)
        : base(dbContext) { }

    public async Task<IReadOnlyList<string>> ListDistinctPermissionNamesBySubjectAsync(
        string subject,
        CancellationToken ct = default
    )
    {
        bool subjectMatchesApplicationUserId = Guid.TryParse(
            subject,
            out Guid subjectAsApplicationUserId
        );
        var spec = new UserByPrincipalSubjectSpecification(
            subject,
            subjectMatchesApplicationUserId,
            subjectAsApplicationUserId
        );

        IQueryable<AppUser> query = ApplySpecification(spec);
        return await query
            .SelectMany(u => u.Roles)
            .SelectMany(r => r.Permissions)
            .Select(rp => rp.Permission)
            .Distinct()
            .ToListAsync(ct);
    }

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default)
    {
        return AnyAsync(new UserByEmailSpecification(email), ct);
    }

    public Task<bool> ExistsByUsernameAsync(
        string normalizedUsername,
        CancellationToken ct = default
    )
    {
        return AnyAsync(new UserByUsernameSpecification(normalizedUsername), ct);
    }

    public Task<AppUser?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        return FirstOrDefaultAsync(new UserByEmailSpecification(email), ct);
    }
}
