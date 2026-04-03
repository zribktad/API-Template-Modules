using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SharedKernel.Domain.Options;

namespace SharedKernel.Infrastructure.UnitOfWork;

/// <summary>
/// Abstracts low-level database transaction management and execution strategy creation.
/// </summary>
public interface IDbTransactionProvider
{
    IDbContextTransaction? CurrentTransaction { get; }

    Task<IDbContextTransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel,
        CancellationToken ct
    );

    IExecutionStrategy CreateExecutionStrategy(TransactionOptions options);
}

/// <summary>
/// A module-scoped transaction provider interface.
/// </summary>
public interface IDbTransactionProvider<TContext> : IDbTransactionProvider
    where TContext : DbContext { }
