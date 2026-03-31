using APITemplate.Infrastructure.Persistence;
using SharedKernel.Domain.Interfaces;

namespace APITemplate.Infrastructure.Repositories;

/// <summary>
/// Base repository that wraps the Ardalis Specification EF Core repository, overriding write methods
/// to stage changes without flushing — persistence is deferred to <see cref="IUnitOfWork.CommitAsync"/>.
/// </summary>
// Generic base repository — T is constrained to class (reference type) so EF Core can track it.
// abstract = cannot be instantiated directly, must be inherited (e.g. ProductRepository : RepositoryBase<Product>).
// SaveChangesAsync is intentionally NOT called here — use IUnitOfWork.CommitAsync() in the service layer.
public abstract class RepositoryBase<T>
    : SharedKernel.Infrastructure.Repositories.RepositoryBase<T>,
        IRepository<T>
    where T : class
{
    protected AppDbContext AppDb => (AppDbContext)DbContext;

    protected RepositoryBase(AppDbContext dbContext)
        : base(dbContext) { }
}
