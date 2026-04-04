using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel.Application.Options.Infrastructure;
using SharedKernel.Domain.Interfaces;
using SharedKernel.Domain.Options;

namespace SharedKernel.Infrastructure.UnitOfWork;

/// <summary>
///     Generic EF Core implementation of <see cref="IUnitOfWork{TContext}" /> backed by a module-specific
///     <see cref="DbContext" />.
/// </summary>
public class UnitOfWork<TContext> : IUnitOfWork<TContext>
    where TContext : DbContext
{
    private const string CommitWithinTransactionMessage =
        "CommitAsync cannot be called inside ExecuteInTransactionAsync. The outermost transaction saves and commits automatically.";

    private readonly DbContextCommandTimeoutScope _commandTimeoutScope;

    private readonly TContext _dbContext;
    private readonly ILogger _logger;
    private readonly ManagedTransactionScope _managedTransactionScope = new();
    private readonly DbContextTrackedStateManager _trackedStateManager;
    private readonly TransactionDefaultsOptions _transactionDefaults;
    private readonly IDbTransactionProvider<TContext> _transactionProvider;
    private TransactionOptions? _activeTransactionOptions;
    private int _savepointCounter;

    public UnitOfWork(
        TContext dbContext,
        IOptions<TransactionDefaultsOptions> transactionDefaults,
        ILogger<UnitOfWork<TContext>> logger,
        IDbTransactionProvider<TContext> transactionProvider
    )
    {
        _dbContext = dbContext;
        _transactionDefaults = transactionDefaults.Value;
        _logger = logger;
        _transactionProvider = transactionProvider;
        _trackedStateManager = new DbContextTrackedStateManager(dbContext);
        _commandTimeoutScope = new DbContextCommandTimeoutScope(dbContext);
    }

    public Task CommitAsync(CancellationToken ct = default)
    {
        if (_managedTransactionScope.IsActive)
        {
            _logger.CommitRejectedInsideManagedTransaction();
            throw new InvalidOperationException(CommitWithinTransactionMessage);
        }

        TransactionOptions effectiveOptions = _transactionDefaults.Resolve(null);
        _logger.CommitStarted(
            effectiveOptions.RetryEnabled ?? true,
            effectiveOptions.TimeoutSeconds
        );
        IExecutionStrategy strategy = _transactionProvider.CreateExecutionStrategy(
            effectiveOptions
        );
        return strategy.ExecuteAsync(
            async cancellationToken =>
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.CommitCompleted();
            },
            ct
        );
    }

    public async Task ExecuteInTransactionAsync(
        Func<Task> action,
        CancellationToken ct = default,
        TransactionOptions? options = null
    )
    {
        await ExecuteInTransactionAsync(
            async () =>
            {
                await action();
                return true;
            },
            ct,
            options
        );
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<Task<T>> action,
        CancellationToken ct = default,
        TransactionOptions? options = null
    )
    {
        IDbContextTransaction? currentTransaction = _transactionProvider.CurrentTransaction;
        if (currentTransaction is not null)
            return await ExecuteWithinSavepointAsync(currentTransaction, action, options, ct);

        TransactionOptions effectiveOptions = _transactionDefaults.Resolve(options);
        return await ExecuteAsOutermostTransactionAsync(action, effectiveOptions, ct);
    }

    private async Task<T> ExecuteWithinSavepointAsync<T>(
        IDbContextTransaction transaction,
        Func<Task<T>> action,
        TransactionOptions? options,
        CancellationToken ct
    )
    {
        ValidateNestedTransactionOptions(options);
        string savepointName = $"uow_sp_{Interlocked.Increment(ref _savepointCounter)}";
        IReadOnlyDictionary<object, DbContextTrackedStateManager.TrackedEntitySnapshot> snapshot =
            _trackedStateManager.Capture();

        _logger.SavepointCreating(savepointName);
        await transaction.CreateSavepointAsync(savepointName, ct);
        try
        {
            using IDisposable scope = _managedTransactionScope.Enter();
            T? result = await action();
            await ReleaseSavepointIfSupportedAsync(transaction, savepointName, ct);
            _logger.SavepointReleased(savepointName);
            return result;
        }
        catch
        {
            await transaction.RollbackToSavepointAsync(savepointName, ct);
            _trackedStateManager.Restore(snapshot);
            _logger.SavepointRolledBack(savepointName);
            throw;
        }
    }

    private async Task<T> ExecuteAsOutermostTransactionAsync<T>(
        Func<Task<T>> action,
        TransactionOptions effectiveOptions,
        CancellationToken ct
    )
    {
        IExecutionStrategy strategy = _transactionProvider.CreateExecutionStrategy(
            effectiveOptions
        );
        TransactionOptions? previousActiveOptions = _activeTransactionOptions;

        return await strategy.ExecuteAsync(
            action,
            async (_, transactionalAction, cancellationToken) =>
            {
                _activeTransactionOptions = effectiveOptions;
                using IDisposable timeoutScope = _commandTimeoutScope.Apply(
                    effectiveOptions.TimeoutSeconds
                );
                _logger.OutermostTransactionStarted(
                    effectiveOptions.IsolationLevel!.Value,
                    effectiveOptions.TimeoutSeconds,
                    effectiveOptions.RetryEnabled ?? true
                );

                IDbContextTransaction? transaction = null;
                try
                {
                    transaction = await _transactionProvider.BeginTransactionAsync(
                        effectiveOptions.IsolationLevel!.Value,
                        cancellationToken
                    );
                    _logger.DatabaseTransactionOpened();
                }
                catch (Exception ex) when (IsTransactionNotSupported(ex))
                {
                    _logger.DatabaseTransactionUnsupported(ex);
                }

                IReadOnlyDictionary<
                    object,
                    DbContextTrackedStateManager.TrackedEntitySnapshot
                > snapshot = _trackedStateManager.Capture();

                try
                {
                    using IDisposable scope = _managedTransactionScope.Enter();
                    T? result = await transactionalAction();
                    await _dbContext.SaveChangesAsync(cancellationToken);

                    if (transaction is not null)
                    {
                        await transaction.CommitAsync(cancellationToken);
                        _logger.DatabaseTransactionCommitted();
                    }

                    _logger.OutermostTransactionCompleted();
                    return result;
                }
                catch (Exception ex)
                {
                    if (transaction is not null)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        _logger.DatabaseTransactionRolledBack(ex);
                    }

                    _trackedStateManager.Restore(snapshot);
                    throw;
                }
                finally
                {
                    if (transaction is not null)
                        await transaction.DisposeAsync();

                    _activeTransactionOptions = previousActiveOptions;
                }
            },
            null,
            ct
        );
    }

    private void ValidateNestedTransactionOptions(TransactionOptions? options)
    {
        if (_activeTransactionOptions is null)
        {
            throw new InvalidOperationException(
                "Nested transaction execution requires an active outer transaction policy."
            );
        }

        if (options is null || options.IsEmpty())
            return;

        TransactionOptions effectiveOptions = _transactionDefaults.Resolve(options);
        if (effectiveOptions != _activeTransactionOptions)
        {
            throw new InvalidOperationException(
                "Nested transactions inherit the active outer transaction options. "
                    + "Pass null/default options inside nested ExecuteInTransactionAsync calls."
            );
        }
    }

    private async Task ReleaseSavepointIfSupportedAsync(
        IDbContextTransaction transaction,
        string savepointName,
        CancellationToken ct
    )
    {
        try
        {
            await transaction.ReleaseSavepointAsync(savepointName, ct);
        }
        catch (NotSupportedException) { }
    }

    private static bool IsTransactionNotSupported(Exception ex)
    {
        return ex is InvalidOperationException or NotSupportedException;
    }
}
