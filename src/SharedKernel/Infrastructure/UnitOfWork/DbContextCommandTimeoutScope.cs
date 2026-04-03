using Microsoft.EntityFrameworkCore;

namespace SharedKernel.Infrastructure.UnitOfWork;

/// <summary>
/// Temporarily overrides the EF Core command timeout for the duration of a scope.
/// </summary>
internal sealed class DbContextCommandTimeoutScope(DbContext dbContext)
{
    public IDisposable Apply(int? timeoutSeconds)
    {
        var previousTimeout = GetCommandTimeoutIfSupported();
        SetCommandTimeoutIfSupported(timeoutSeconds);
        return new Releaser(this, previousTimeout);
    }

    private int? GetCommandTimeoutIfSupported()
    {
        try
        {
            return dbContext.Database.GetCommandTimeout();
        }
        catch (Exception ex) when (IsCommandTimeoutNotSupported(ex))
        {
            return null;
        }
    }

    private void SetCommandTimeoutIfSupported(int? timeoutSeconds)
    {
        try
        {
            dbContext.Database.SetCommandTimeout(timeoutSeconds);
        }
        catch (Exception ex) when (IsCommandTimeoutNotSupported(ex)) { }
    }

    private static bool IsCommandTimeoutNotSupported(Exception ex) =>
        ex is InvalidOperationException or NotSupportedException;

    private sealed class Releaser(DbContextCommandTimeoutScope scope, int? previousTimeout)
        : IDisposable
    {
        private DbContextCommandTimeoutScope? _scope = scope;

        public void Dispose()
        {
            DbContextCommandTimeoutScope? scope = Interlocked.Exchange(ref _scope, null);
            if (scope is null)
                return;

            scope.SetCommandTimeoutIfSupported(previousTimeout);
        }
    }
}
