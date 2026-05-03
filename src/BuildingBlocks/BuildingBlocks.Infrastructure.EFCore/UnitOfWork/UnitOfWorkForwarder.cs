using BuildingBlocks.Domain.Interfaces;
using BuildingBlocks.Domain.Options;

namespace BuildingBlocks.Infrastructure.EFCore.UnitOfWork;

/// <summary>
///     Forwards <see cref="IUnitOfWork{TMarker}" /> calls to an existing <see cref="IUnitOfWork" /> instance.
///     Used to register a Domain-layer marker type as a DI discriminator that resolves to the real
///     <see cref="UnitOfWork{TContext}" /> backed by the module's EF Core DbContext.
/// </summary>
/// <typeparam name="TMarker">
///     Domain-layer marker type that identifies the module's persistence boundary.
/// </typeparam>
public sealed class UnitOfWorkForwarder<TMarker> : IUnitOfWork<TMarker>
{
    private readonly IUnitOfWork _inner;

    public UnitOfWorkForwarder(IUnitOfWork inner)
    {
        _inner = inner;
    }

    public Task CommitAsync(CancellationToken ct = default)
    {
        return _inner.CommitAsync(ct);
    }

    public Task ExecuteInTransactionAsync(
        Func<Task> action,
        CancellationToken ct = default,
        TransactionOptions? options = null
    )
    {
        return _inner.ExecuteInTransactionAsync(action, ct, options);
    }

    public Task<T> ExecuteInTransactionAsync<T>(
        Func<Task<T>> action,
        CancellationToken ct = default,
        TransactionOptions? options = null
    )
    {
        return _inner.ExecuteInTransactionAsync(action, ct, options);
    }
}

