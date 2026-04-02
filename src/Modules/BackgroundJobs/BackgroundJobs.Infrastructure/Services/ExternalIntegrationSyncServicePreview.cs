using BackgroundJobs.Application.Services;
using BackgroundJobs.Infrastructure.Logging;
using Microsoft.Extensions.Logging;

namespace BackgroundJobs.Infrastructure.Services;

/// <summary>
/// Placeholder implementation of <see cref="IExternalIntegrationSyncService"/> used until
/// a real provider-specific synchronization workflow is registered.
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

    public Task SynchronizeAsync(CancellationToken ct = default)
    {
        _logger.ExternalIntegrationSyncNoProviderRegistered();
        return Task.CompletedTask;
    }
}
