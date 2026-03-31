using System.Data;
using APITemplate.Domain.Options;
using Microsoft.EntityFrameworkCore.Storage;

namespace APITemplate.Infrastructure.Persistence;

/// <summary>
/// Abstracts low-level database transaction management and execution strategy creation
/// used by <see cref="UnitOfWork"/> to operate independently of the specific EF Core provider.
/// </summary>
public interface IDbTransactionProvider
{
    /// <summary>Returns the currently active database transaction, or <c>null</c> when none is open.</summary>
    IDbContextTransaction? CurrentTransaction { get; }

    /// <summary>Opens a new database transaction with the specified isolation level.</summary>
    Task<IDbContextTransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel,
        CancellationToken ct
    );

    /// <summary>Creates an execution strategy appropriate for the current provider and the given transaction options.</summary>
    IExecutionStrategy CreateExecutionStrategy(TransactionOptions options);
}
