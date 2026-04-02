using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Logging;
using SharedKernel.Application.Context;

namespace APITemplate.Api.Cache;

public sealed class OutputCacheInvalidationService(
    IOutputCacheStore outputCacheStore,
    ITenantProvider tenantProvider,
    ILogger<OutputCacheInvalidationService> logger)
    : IOutputCacheInvalidationService
{
    public Task EvictAsync(string tag, CancellationToken cancellationToken = default) =>
        EvictAsync([tag], cancellationToken);

    public async Task EvictAsync(
        IEnumerable<string> tags,
        CancellationToken cancellationToken = default
    )
    {
        var tenantSuffix = tenantProvider.HasTenant ? $"-{tenantProvider.TenantId}" : string.Empty;

        foreach (string tag in tags.Distinct(StringComparer.Ordinal))
        {
            var targetTag = $"{tag}{tenantSuffix}";
            try
            {
                await outputCacheStore.EvictByTagAsync(targetTag, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to evict output cache for tag: {Tag}", targetTag);
            }
        }
    }
}
