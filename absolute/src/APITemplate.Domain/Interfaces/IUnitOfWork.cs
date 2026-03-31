using APITemplate.Domain.Options;

namespace APITemplate.Domain.Interfaces;

/// <summary>
/// Contract for the relational unit-of-work boundary used by application services.
/// Repositories stage entity changes, while this contract defines how those staged changes are flushed
/// and how explicit transaction boundaries are created.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CommitAsync"/> is the simple write path for already-orchestrated service operations and
/// translates to one persistence flush for the current scope.
/// </para>
/// <para>
/// <see cref="ExecuteInTransactionAsync{T}(Func{Task{T}}, CancellationToken, TransactionOptions?)"/>
/// is the explicit transaction path. The outermost call resolves the effective transaction policy by merging
/// configured defaults with per-call overrides, applies the effective timeout/retry policy, opens the database
/// transaction, and commits once after the delegate succeeds.
/// </para>
/// <para>
/// Nested <c>ExecuteInTransactionAsync(...)</c> calls do not create another top-level transaction. They execute
/// inside the active outer transaction by using a savepoint and inherit the active outer policy. Conflicting nested
/// options fail fast to avoid silently changing isolation level, timeout, or retry behavior mid-transaction.
/// </para>
/// </remarks>
public interface IUnitOfWork
{
    /// <summary>
    /// Persists all staged relational changes for the current service operation.
    /// Use this for single-write flows after repository calls.
    /// This method must not be called inside <see cref="ExecuteInTransactionAsync(Func{Task}, CancellationToken, TransactionOptions?)"/>
    /// or <see cref="ExecuteInTransactionAsync{T}(Func{Task{T}}, CancellationToken, TransactionOptions?)"/>.
    /// </summary>
    Task CommitAsync(CancellationToken ct = default);

    /// <summary>
    /// Runs a multi-step relational write flow in one explicit transaction.
    /// The outermost call owns the database transaction and retry strategy; nested calls use savepoints inside the active transaction.
    /// The delegate should stage repository changes only; do not call <see cref="CommitAsync"/> inside it.
    /// Calling <see cref="CommitAsync"/> from inside the delegate throws <see cref="InvalidOperationException"/>.
    /// When <paramref name="options"/> is provided, its non-null values override the configured transaction defaults for the outermost call.
    /// Nested calls inherit the active outer transaction policy and must not pass conflicting overrides.
    /// Example:
    /// await _unitOfWork.ExecuteInTransactionAsync(async () =>
    /// {
    ///     await _productRepository.UpdateAsync(product, ct);
    ///     await _reviewRepository.AddAsync(review, ct);
    /// }, ct);
    /// </summary>
    Task ExecuteInTransactionAsync(
        Func<Task> action,
        CancellationToken ct = default,
        TransactionOptions? options = null);

    /// <summary>
    /// Runs a multi-step relational write flow in one explicit transaction and returns a value.
    /// The outermost call owns the database transaction and retry strategy; nested calls use savepoints inside the active transaction.
    /// The delegate should stage repository changes only; do not call <see cref="CommitAsync"/> inside it.
    /// Calling <see cref="CommitAsync"/> from inside the delegate throws <see cref="InvalidOperationException"/>.
    /// When <paramref name="options"/> is provided, its non-null values override the configured transaction defaults for the outermost call.
    /// Nested calls inherit the active outer transaction policy and must not pass conflicting overrides.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(
        Func<Task<T>> action,
        CancellationToken ct = default,
        TransactionOptions? options = null);
}
