using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SharedKernel.Domain.Options;

namespace SharedKernel.Infrastructure.UnitOfWork;

/// <summary>
///     Abstracts low-level database transaction management and execution strategy creation.
/// </summary>
public interface IDbTransactionProvider
{
    public IDbContextTransaction? CurrentTransaction { get; }

    public Task<IDbContextTransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel,
        CancellationToken ct
    );

    public IExecutionStrategy CreateExecutionStrategy(TransactionOptions options);
}

/// <summary>
///     A module-scoped transaction provider interface.
/// </summary>
public interface IDbTransactionProvider<TContext> : IDbTransactionProvider
    where TContext : DbContext { }
