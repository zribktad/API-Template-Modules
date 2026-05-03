using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using BuildingBlocks.Domain.Options;

namespace BuildingBlocks.Infrastructure.EFCore.UnitOfWork;

/// <summary>
///     EF Core implementation of <see cref="IDbTransactionProvider" /> backed by a generic <see cref="DbContext" />.
/// </summary>
public class EfCoreTransactionProvider : IDbTransactionProvider
{
    private readonly DbContext _dbContext;

    public EfCoreTransactionProvider(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IDbContextTransaction? CurrentTransaction => _dbContext.Database.CurrentTransaction;

    public Task<IDbContextTransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel,
        CancellationToken ct
    )
    {
        return _dbContext.Database.BeginTransactionAsync(isolationLevel, ct);
    }

    public IExecutionStrategy CreateExecutionStrategy(TransactionOptions options)
    {
        return UnitOfWorkExecutionStrategyFactory.Create(_dbContext, options);
    }
}

/// <summary>
///     EF Core implementation of <see cref="IDbTransactionProvider{TContext}" /> backed by a specific module's
///     <see cref="DbContext" />.
/// </summary>
public class EfCoreTransactionProvider<TContext>
    : EfCoreTransactionProvider,
        IDbTransactionProvider<TContext>
    where TContext : DbContext
{
    public EfCoreTransactionProvider(TContext dbContext)
        : base(dbContext) { }
}

