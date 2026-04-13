using Identity.Directory.Entities;
using Identity.Directory.Interfaces;
using Identity.Persistence;

namespace Identity.Directory.Repositories;

internal sealed class RoleRepository : RepositoryBase<CustomRole>, IRoleRepository
{
    public RoleRepository(IdentityDbContext dbContext)
        : base(dbContext) { }
}
