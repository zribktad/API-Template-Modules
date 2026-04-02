using APITemplate.Application.Common.BackgroundJobs;
using Microsoft.Extensions.Logging;

namespace APITemplate.Infrastructure.BackgroundJobs.Services;

/// <summary>
/// Placeholder implementation of <see cref="IExternalIntegrationSyncService"/> used until
/// a real provider-specific synchronization workflow is registered.
/// Logs a warning and returns immediately without performing any work.
/// </summary>
public sealed class ExternalIntegrationSyncServicePreview : IExternalIntegrationSyncService
{
    private readonly ILogger<ExternalIntegrationSyncServicePreview> _logger;

    public ExternalIntegrationSyncServicePreview(
        ILogger<ExternalIntegrationSyncServicePreview> logger
    )
    {
        _logger = logger;
    }

    /// <summary>Logs a notice that no sync workflow is registered and completes without error.</summary>
    public Task SynchronizeAsync(CancellationToken ct = default)
    {
        _logger.LogInformation(
            "External integration synchronization job executed, but no provider-specific synchronization workflow is registered yet."
        );
        return Task.CompletedTask;
    }
}
