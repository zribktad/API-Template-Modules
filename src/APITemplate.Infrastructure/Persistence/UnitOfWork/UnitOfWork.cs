using System.Data;
using APITemplate.Application.Common.Options;
using APITemplate.Domain.Interfaces;
using APITemplate.Domain.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace APITemplate.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IUnitOfWork"/> backed by <see cref="AppDbContext"/>.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private const string CommitWithinTransactionMessage =
        "CommitAsync cannot be called inside ExecuteInTransactionAsync. The outermost transaction saves and commits automatically.";

    private readonly AppDbContext _dbContext;
    private readonly TransactionDefaultsOptions _transactionDefaults;
    private readonly ILogger<UnitOfWork> _logger;
    private readonly IDbTransactionProvider _transactionProvider;
    private readonly ManagedTransactionScope _managedTransactionScope = new();
    private readonly DbContextTrackedStateManager _trackedStateManager;
    private readonly DbContextCommandTimeoutScope _commandTimeoutScope;
    private int _savepointCounter;
    private TransactionOptions? _activeTransactionOptions;

    /// <summary>
    /// Creates a <see cref="UnitOfWork"/> that uses configured transaction defaults for explicit transactions.
    /// </summary>
    /// <param name="dbContext">EF Core context that tracks staged relational changes for the current scope.</param>
    /// <param name="transactionDefaults">
    /// Configured defaults used to resolve the effective isolation level, timeout, and retry policy
    /// for outermost <see cref="ExecuteInTransactionAsync"/> calls.
    /// </param>
    /// <param name="logger">Logger used for transaction orchestration diagnostics.</param>
    /// <param name="transactionProvider">Provides transaction management operations for the underlying database.</param>
    public UnitOfWork(
        AppDbContext dbContext,
        IOptions<TransactionDefaultsOptions> transactionDefaults,
        ILogger<UnitOfWork> logger,
        IDbTransactionProvider transactionProvider)
    {
        _dbContext = dbContext;
        _transactionDefaults = transactionDefaults.Value;
        _logger = logger;
        _transactionProvider = transactionProvider;
        _trackedStateManager = new DbContextTrackedStateManager(dbContext);
        _commandTimeoutScope = new DbContextCommandTimeoutScope(dbContext);
    }

    /// <summary>
    /// Persists all currently staged relational changes without opening an explicit transaction boundary.
    /// Use this for simple service flows that already know when the write should be flushed.
    /// Retries are managed by this unit of work using the configured default transaction policy.
    /// </summary>
    /// <param name="ct">Cancellation token for the underlying <c>SaveChangesAsync</c> call.</param>
    /// <returns>A task that completes when all staged changes have been flushed to the database.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when called inside <see cref="ExecuteInTransactionAsync"/> because the outermost managed transaction
    /// owns the save and commit lifecycle.
    /// </exception>
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
            ct);
    }

    /// <summary>
    /// Executes a write delegate inside an explicit relational transaction.
    /// The outermost call owns transaction creation, retry strategy, timeout application, save, and commit.
    /// </summary>
    /// <param name="action">
    /// Delegate that stages repository/entity changes inside the transaction boundary.
    /// The delegate should not call <see cref="CommitAsync"/>.
    /// </param>
    /// <param name="ct">Cancellation token propagated to transaction, savepoint, and save operations.</param>
    /// <param name="options">
    /// Optional per-call transaction overrides. Non-null values override configured defaults only for the outermost call.
    /// Nested calls inherit the already active outer transaction policy.
    /// </param>
    /// <returns>A task that completes when the transactional delegate has been saved and committed.</returns>
    public async Task ExecuteInTransactionAsync(
        Func<Task> action,
        CancellationToken ct = default,
        TransactionOptions? options = null)
        => await ExecuteInTransactionAsync(
            async () =>
            {
                await action();
                return true;
            },
            ct,
            options);

    /// <summary>
    /// Executes a write delegate inside an explicit relational transaction and returns a value created by that flow.
    /// Per-call <paramref name="options"/> override configured defaults only for the outermost transaction boundary.
    /// </summary>
    /// <remarks>
    /// Do not call <see cref="CommitAsync"/> inside <paramref name="action"/>. The outermost transaction saves and commits
    /// after the delegate completes successfully, and nested calls use savepoints only.
    /// </remarks>
    /// <typeparam name="T">Type returned by the transactional delegate.</typeparam>
    /// <param name="action">
    /// Delegate that stages repository/entity changes and returns a value computed inside the transaction boundary.
    /// </param>
    /// <param name="ct">Cancellation token propagated to transaction, savepoint, and save operations.</param>
    /// <param name="options">
    /// Optional per-call transaction overrides. Non-null values override configured defaults only for the outermost call.
    /// Nested calls inherit the already active outer transaction policy.
    /// </param>
    /// <returns>The value returned by <paramref name="action"/> after the transaction has been saved and committed.</returns>
    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<Task<T>> action,
        CancellationToken ct = default,
        TransactionOptions? options = null)
    {
        var currentTransaction = _transactionProvider.CurrentTransaction;
        if (currentTransaction is not null)
            return await ExecuteWithinSavepointAsync(currentTransaction, action, options, ct);

        var effectiveOptions = _transactionDefaults.Resolve(options);
        return await ExecuteAsOutermostTransactionAsync(action, effectiveOptions, ct);
    }

    /// <summary>
    /// Executes a nested transaction scope by using a savepoint inside the active outer transaction.
    /// Only null/default nested options are allowed unless they resolve to the same effective outer policy.
    /// </summary>
    /// <typeparam name="T">Type returned by the nested delegate.</typeparam>
    /// <param name="transaction">Currently active outer database transaction.</param>
    /// <param name="action">Nested delegate executed under a savepoint inside <paramref name="transaction"/>.</param>
    /// <param name="options">
    /// Optional nested overrides. Conflicting values are rejected because nested scopes cannot redefine
    /// the already active outer transaction policy.
    /// </param>
    /// <param name="ct">Cancellation token propagated to savepoint operations.</param>
    /// <returns>The value returned by <paramref name="action"/> when the nested scope succeeds.</returns>
    private async Task<T> ExecuteWithinSavepointAsync<T>(
        IDbContextTransaction transaction,
        Func<Task<T>> action,
        TransactionOptions? options,
        CancellationToken ct)
    {
        ValidateNestedTransactionOptions(options);
        var savepointName = $"uow_sp_{Interlocked.Increment(ref _savepointCounter)}";
        var snapshot = _trackedStateManager.Capture();

        // Nested work reuses the active transaction and isolates rollback via a savepoint.
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

    /// <summary>
    /// Executes the outermost transaction boundary through EF Core's execution strategy so the whole unit
    /// of work can be replayed on transient relational failures.
    /// </summary>
    /// <typeparam name="T">Type returned by the transactional delegate.</typeparam>
    /// <param name="action">Delegate that stages all relational changes for the outer transaction.</param>
    /// <param name="effectiveOptions">Resolved transaction policy after config defaults and per-call overrides are merged.</param>
    /// <param name="ct">Cancellation token propagated to strategy, transaction, and save operations.</param>
    /// <returns>The value returned by <paramref name="action"/> after the transaction commits successfully.</returns>
    private async Task<T> ExecuteAsOutermostTransactionAsync<T>(
        Func<Task<T>> action,
        TransactionOptions effectiveOptions,
        CancellationToken ct)
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
                    effectiveOptions.RetryEnabled ?? true);

                IDbContextTransaction? transaction = null;
                try
                {
                    transaction = await _transactionProvider.BeginTransactionAsync(
                        effectiveOptions.IsolationLevel!.Value,
                        cancellationToken);
                    _logger.DatabaseTransactionOpened();
                }
                catch (Exception ex) when (IsTransactionNotSupported(ex))
                {
                    // Providers without transaction support still use the same unit-of-work flow,
                    // but save without an explicit database transaction.
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
            ct);
    }

    /// <summary>
    /// Ensures nested transaction scopes inherit the effective outer transaction policy.
    /// </summary>
    /// <param name="options">Optional nested transaction overrides.</param>
    private void ValidateNestedTransactionOptions(TransactionOptions? options)
    {
        if (_activeTransactionOptions is null)
            throw new InvalidOperationException("Nested transaction execution requires an active outer transaction policy.");

        if (options is null || options.IsEmpty())
            return;

        var effectiveOptions = _transactionDefaults.Resolve(options);
        if (effectiveOptions != _activeTransactionOptions)
        {
            throw new InvalidOperationException(
                "Nested transactions inherit the active outer transaction options. " +
                "Pass null/default options inside nested ExecuteInTransactionAsync calls.");
        }
    }

    /// <summary>
    /// Releases the current savepoint when the provider supports explicit savepoint release.
    /// Some providers treat savepoint release as optional, so unsupported cases are ignored.
    /// </summary>
    /// <param name="transaction">Active database transaction that owns the savepoint.</param>
    /// <param name="savepointName">Provider-specific name of the savepoint to release.</param>
    /// <param name="ct">Cancellation token propagated to the provider.</param>
    private async Task ReleaseSavepointIfSupportedAsync(
        IDbContextTransaction transaction,
        string savepointName,
        CancellationToken ct)
    {
        try
        {
            await transaction.ReleaseSavepointAsync(savepointName, ct);
        }
        catch (NotSupportedException)
        {
        }
    }

    private static bool IsTransactionNotSupported(Exception ex)
        => ex is InvalidOperationException or NotSupportedException;
}
