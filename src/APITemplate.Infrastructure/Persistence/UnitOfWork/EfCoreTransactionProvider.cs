namespace APITemplate.Infrastructure.Persistence;

/// <summary>
/// Legacy namespace wrapper for the SharedKernel EF Core transaction provider.
/// </summary>
public sealed class EfCoreTransactionProvider
    : SharedKernel.Infrastructure.UnitOfWork.EfCoreTransactionProvider,
        IDbTransactionProvider
{
    public EfCoreTransactionProvider(AppDbContext dbContext)
        : base(dbContext) { }
}
