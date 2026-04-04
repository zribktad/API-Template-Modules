namespace SharedKernel.Infrastructure.UnitOfWork;

/// <summary>
///     Tracks the nesting depth of managed transaction scopes opened by the unit of work.
/// </summary>
internal sealed class ManagedTransactionScope
{
    private int _depth;

    public bool IsActive => Volatile.Read(ref _depth) > 0;

    public IDisposable Enter()
    {
        Interlocked.Increment(ref _depth);
        return new Releaser(this);
    }

    private void Exit()
    {
        Interlocked.Decrement(ref _depth);
    }

    private sealed class Releaser(ManagedTransactionScope scope) : IDisposable
    {
        private ManagedTransactionScope? _scope = scope;

        public void Dispose()
        {
            ManagedTransactionScope? scope = Interlocked.Exchange(ref _scope, null);
            scope?.Exit();
        }
    }
}
