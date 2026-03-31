using System.Data;
using SharedKernel.Domain.Options;
using Microsoft.EntityFrameworkCore.Storage;

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
