using BuildingBlocks.Domain.Interfaces;

namespace BuildingBlocks.Infrastructure.EFCore.StoredProcedures;

/// <summary>
///     Forwards <see cref="IStoredProcedureExecutor{TMarker}" /> calls to the real, context-bound
///     <see cref="IStoredProcedureExecutor" />.
/// </summary>
public sealed class StoredProcedureExecutorForwarder<TMarker> : IStoredProcedureExecutor<TMarker>
{
    private readonly IStoredProcedureExecutor _inner;

    public StoredProcedureExecutorForwarder(IStoredProcedureExecutor inner)
    {
        _inner = inner;
    }

    public Task<TResult?> QueryFirstAsync<TResult>(
        IStoredProcedure<TResult> procedure,
        CancellationToken ct = default
    )
        where TResult : class => _inner.QueryFirstAsync(procedure, ct);

    public Task<IReadOnlyList<TResult>> QueryManyAsync<TResult>(
        IStoredProcedure<TResult> procedure,
        CancellationToken ct = default
    )
        where TResult : class => _inner.QueryManyAsync(procedure, ct);

    public Task<TResult?> ScalarFirstAsync<TResult>(
        IScalarStoredProcedure<TResult> procedure,
        CancellationToken ct = default
    ) => _inner.ScalarFirstAsync(procedure, ct);

    public Task<IReadOnlyList<TResult>> ScalarManyAsync<TResult>(
        IScalarStoredProcedure<TResult> procedure,
        CancellationToken ct = default
    ) => _inner.ScalarManyAsync(procedure, ct);

    public Task<int> ExecuteAsync(FormattableString sql, CancellationToken ct = default) =>
        _inner.ExecuteAsync(sql, ct);
}
