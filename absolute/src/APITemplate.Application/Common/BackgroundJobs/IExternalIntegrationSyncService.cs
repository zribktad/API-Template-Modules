namespace APITemplate.Application.Common.BackgroundJobs;

/// <summary>
/// Application-layer contract for synchronizing data with external third-party integrations.
/// Implementations are provided by the Infrastructure layer and scheduled as recurring background jobs.
/// </summary>
public interface IExternalIntegrationSyncService
{
    /// <summary>
    /// Pulls changes from external systems and reconciles them with the local data store.
    /// </summary>
    Task SynchronizeAsync(CancellationToken ct = default);
}
