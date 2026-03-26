namespace APITemplate.Application.Common.Startup;

/// <summary>
/// Enumerates the named startup tasks whose distributed execution is coordinated via
/// <see cref="IStartupTaskCoordinator"/>. Values are numeric identifiers that act as
/// stable distributed lock keys.
/// </summary>
public enum StartupTaskName : long
{
    AppBootstrap = 2026031801,
    BackgroundJobsBootstrap = 2026031802,
}
