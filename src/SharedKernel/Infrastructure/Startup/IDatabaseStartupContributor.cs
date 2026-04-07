namespace SharedKernel.Infrastructure.Startup;

/// <summary>
///     Module hook invoked during host database startup (e.g. Development schema ensure / migrations).
/// </summary>
public interface IDatabaseStartupContributor
{
    /// <summary>Lower values run first.</summary>
    int Order { get; }

    Task ApplyAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default
    );
}
