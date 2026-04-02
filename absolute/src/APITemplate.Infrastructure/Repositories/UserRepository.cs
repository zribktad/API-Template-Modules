using APITemplate.Application.Features.User.Specifications;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Persistence;

namespace APITemplate.Infrastructure.Repositories;

/// <summary>EF Core repository for <see cref="AppUser"/> with specification-based lookup by email and username.</summary>
public sealed class UserRepository : RepositoryBase<AppUser>, IUserRepository
{
    public UserRepository(AppDbContext dbContext)
        : base(dbContext) { }

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default) =>
        AnyAsync(new UserByEmailSpecification(email), ct);

    public Task<bool> ExistsByUsernameAsync(
        string normalizedUsername,
        CancellationToken ct = default
    ) => AnyAsync(new UserByUsernameSpecification(normalizedUsername), ct);

    public Task<AppUser?> FindByEmailAsync(string email, CancellationToken ct = default) =>
        FirstOrDefaultAsync(new UserByEmailSpecification(email), ct);
}
