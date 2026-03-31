using SharedKernel.Application.Options.Infrastructure;
using SharedKernel.Domain.Interfaces;
using SharedKernel.Domain.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SharedKernel.Infrastructure.UnitOfWork;

/// <summary>
/// Generic EF Core implementation of <see cref="IUnitOfWork{TContext}"/> backed by a module-specific <see cref="DbContext"/>.
/// </summary>
public class UnitOfWork<TContext> : IUnitOfWork<TContext>
    where TContext : DbContext
{
    private const string CommitWithinTransactionMessage =
        "CommitAsync cannot be called inside ExecuteInTransactionAsync. The outermost transaction saves and commits automatically.";

    private readonly TContext _dbContext;
    private readonly TransactionDefaultsOptions _transactionDefaults;
    private readonly ILogger _logger;
    private readonly IDbTransactionProvider _transactionProvider;
    private readonly ManagedTransactionScope _managedTransactionScope = new();
    private readonly DbContextTrackedStateManager _trackedStateManager;
    private readonly DbContextCommandTimeoutScope _commandTimeoutScope;
    private int _savepointCounter;
    private TransactionOptions? _activeTransactionOptions;

    public UnitOfWork(
        TContext dbContext,
        IOptions<TransactionDefaultsOptions> transactionDefaults,
        ILogger logger,
        IDbTransactionProvider transactionProvider
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

        var effectiveOptions = _transactionDefaults.Resolve(null);
        _logger.CommitStarted(effectiveOptions.RetryEnabled ?? true, effectiveOptions.TimeoutSeconds);
        var strategy = _transactionProvider.CreateExecutionStrategy(effectiveOptions);
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
    ) =>
        await ExecuteInTransactionAsync(
            async () =>
            {
                await action();
                return true;
            },
            ct,
            options
        );

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<Task<T>> action,
        CancellationToken ct = default,
        TransactionOptions? options = null
    )
    {
        var currentTransaction = _transactionProvider.CurrentTransaction;
        if (currentTransaction is not null)
            return await ExecuteWithinSavepointAsync(currentTransaction, action, options, ct);

        var effectiveOptions = _transactionDefaults.Resolve(options);
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
        var savepointName = $"uow_sp_{Interlocked.Increment(ref _savepointCounter)}";
        var snapshot = _trackedStateManager.Capture();

        _logger.SavepointCreating(savepointName);
        await transaction.CreateSavepointAsync(savepointName, ct);
        try
        {
            using var scope = _managedTransactionScope.Enter();
            var result = await action();
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
        var strategy = _transactionProvider.CreateExecutionStrategy(effectiveOptions);
        var previousActiveOptions = _activeTransactionOptions;

        return await strategy.ExecuteAsync(
            state: action,
            operation: async (_, transactionalAction, cancellationToken) =>
            {
                _activeTransactionOptions = effectiveOptions;
                using var timeoutScope = _commandTimeoutScope.Apply(effectiveOptions.TimeoutSeconds);
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

                var snapshot = _trackedStateManager.Capture();

                try
                {
                    using var scope = _managedTransactionScope.Enter();
                    var result = await transactionalAction();
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
            verifySucceeded: null,
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

        var effectiveOptions = _transactionDefaults.Resolve(options);
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
        catch (NotSupportedException)
        {
        }
    }

    private static bool IsTransactionNotSupported(Exception ex) =>
        ex is InvalidOperationException or NotSupportedException;
}
