using System.Data;
using SharedKernel.Domain.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace SharedKernel.Infrastructure.UnitOfWork;

/// <summary>
/// EF Core implementation of <see cref="IDbTransactionProvider"/> backed by a generic <see cref="DbContext"/>.
/// </summary>
public class EfCoreTransactionProvider : IDbTransactionProvider
{
    private readonly DbContext _dbContext;

    public EfCoreTransactionProvider(DbContext dbContext) => _dbContext = dbContext;

    public IDbContextTransaction? CurrentTransaction => _dbContext.Database.CurrentTransaction;

    public Task<IDbContextTransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel,
        CancellationToken ct
    ) => _dbContext.Database.BeginTransactionAsync(isolationLevel, ct);

    public IExecutionStrategy CreateExecutionStrategy(TransactionOptions options) =>
        UnitOfWorkExecutionStrategyFactory.Create(_dbContext, options);
}
