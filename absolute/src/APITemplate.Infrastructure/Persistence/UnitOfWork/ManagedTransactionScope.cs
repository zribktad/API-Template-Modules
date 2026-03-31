namespace APITemplate.Infrastructure.Persistence;

/// <summary>
/// Tracks the nesting depth of managed transaction scopes opened by <see cref="UnitOfWork"/>,
/// exposing <see cref="IsActive"/> to prevent <c>CommitAsync</c> calls inside an outermost transaction.
/// </summary>
internal sealed class ManagedTransactionScope
{
    private int _depth;

    public bool IsActive => Volatile.Read(ref _depth) > 0;

    /// <summary>Increments the nesting depth and returns a disposable that decrements it on disposal.</summary>
    public IDisposable Enter()
    {
        Interlocked.Increment(ref _depth);
        return new Releaser(this);
    }

    private void Exit() => Interlocked.Decrement(ref _depth);

    private sealed class Releaser(ManagedTransactionScope scope) : IDisposable
    {
        private ManagedTransactionScope? _scope = scope;

        public void Dispose()
        {
            var scope = Interlocked.Exchange(ref _scope, null);
            scope?.Exit();
        }
    }
}
