using System.Data;
using APITemplate.Domain.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace APITemplate.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IDbTransactionProvider"/> that delegates transaction
/// management and execution strategy creation to the underlying <see cref="AppDbContext"/>.
/// </summary>
public sealed class EfCoreTransactionProvider : IDbTransactionProvider
{
    private readonly DbContext _dbContext;

    public EfCoreTransactionProvider(AppDbContext dbContext) => _dbContext = dbContext;

    public IDbContextTransaction? CurrentTransaction => _dbContext.Database.CurrentTransaction;

    public Task<IDbContextTransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel,
        CancellationToken ct
    ) => _dbContext.Database.BeginTransactionAsync(isolationLevel, ct);

    public IExecutionStrategy CreateExecutionStrategy(TransactionOptions options) =>
        UnitOfWorkExecutionStrategyFactory.Create(_dbContext, options);
}
