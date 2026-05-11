using System.Data;
using BuildingBlocks.Domain.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BuildingBlocks.Infrastructure.EFCore.UnitOfWork;

/// <summary>
///     Abstracts low-level database transaction management and execution strategy creation.
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
///     A module-scoped transaction provider interface.
/// </summary>
public interface IDbTransactionProvider<TContext> : IDbTransactionProvider
    where TContext : DbContext { }
