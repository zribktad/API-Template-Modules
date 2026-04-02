namespace APITemplate.Application.Common.Startup;

/// <summary>
/// Coordinates one-time startup tasks across multiple application instances using distributed locking,
/// ensuring that tasks such as database seeding run exactly once even in a scaled-out environment.
/// </summary>
public interface IStartupTaskCoordinator
{
    /// <summary>
    /// Acquires an exclusive distributed lease for <paramref name="startupTask"/> and returns
    /// an <see cref="IAsyncDisposable"/> that releases the lease when disposed.
    /// </summary>
    Task<IAsyncDisposable> AcquireAsync(
        StartupTaskName startupTask,
        CancellationToken ct = default
    );
}
