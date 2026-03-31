using Microsoft.EntityFrameworkCore;

namespace APITemplate.Infrastructure.Persistence;

/// <summary>
/// Temporarily overrides the EF Core command timeout for the duration of a scope,
/// restoring the previous timeout value when disposed.
/// </summary>
internal sealed class DbContextCommandTimeoutScope(AppDbContext dbContext)
{
    /// <summary>
    /// Applies <paramref name="timeoutSeconds"/> as the current command timeout and returns a disposable
    /// that restores the previous timeout on disposal. Providers that do not support command timeouts
    /// are silently ignored.
    /// </summary>
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
            var scope = Interlocked.Exchange(ref _scope, null);
            if (scope is null)
                return;

            scope.SetCommandTimeoutIfSupported(previousTimeout);
        }
    }
}
