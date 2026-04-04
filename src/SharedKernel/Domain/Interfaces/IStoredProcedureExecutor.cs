namespace SharedKernel.Domain.Interfaces;

/// <summary>
///     Executes stored procedures and maps the result set to strongly-typed objects.
///     Abstracts the EF Core plumbing (FromSql, DbSet) away from repositories,
///     making the data-access layer easier to test and reason about.
/// </summary>
public interface IStoredProcedureExecutor
{
    /// <summary>
    ///     Executes a procedure and returns the first matching row, or <c>null</c>
    ///     when the result set is empty.
    /// </summary>
    public Task<TResult?> QueryFirstAsync<TResult>(
        IStoredProcedure<TResult> procedure,
        CancellationToken ct = default
    )
        where TResult : class;

    /// <summary>
    ///     Executes a procedure and returns all rows as a read-only list.
    /// </summary>
    public Task<IReadOnlyList<TResult>> QueryManyAsync<TResult>(
        IStoredProcedure<TResult> procedure,
        CancellationToken ct = default
    )
        where TResult : class;

    /// <summary>
    ///     Executes a scalar procedure and returns the first value, or the default of
    ///     <typeparamref name="TResult" /> when the result set is empty.
    /// </summary>
    public Task<TResult?> ScalarFirstAsync<TResult>(
        IScalarStoredProcedure<TResult> procedure,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Executes a scalar procedure and returns all values as a read-only list.
    /// </summary>
    public Task<IReadOnlyList<TResult>> ScalarManyAsync<TResult>(
        IScalarStoredProcedure<TResult> procedure,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Executes a procedure that performs a write operation (INSERT / UPDATE / DELETE)
    ///     and returns the number of affected rows.
    /// </summary>
    public Task<int> ExecuteAsync(FormattableString sql, CancellationToken ct = default);
}
