using Identity.Application.Features.User.Specifications;
using Identity.Infrastructure.Persistence;

namespace Identity.Infrastructure.Repositories;

/// <summary>EF Core repository for <see cref="AppUser"/> with specification-based lookup by email and username.</summary>
public sealed class UserRepository : RepositoryBase<AppUser>, IUserRepository
{
    public UserRepository(IdentityDbContext dbContext)
        : base(dbContext) { }

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default) =>
        AnyAsync(new UserByEmailSpecification(email), ct);

    public Task<bool> ExistsByUsernameAsync(string normalizedUsername, CancellationToken ct = default) =>
        AnyAsync(new UserByUsernameSpecification(normalizedUsername), ct);

    public Task<AppUser?> FindByEmailAsync(string email, CancellationToken ct = default) =>
        FirstOrDefaultAsync(new UserByEmailSpecification(email), ct);
}
