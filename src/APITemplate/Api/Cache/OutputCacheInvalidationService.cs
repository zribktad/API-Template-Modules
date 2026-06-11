using APITemplate.Api;
using Microsoft.AspNetCore.OutputCaching;

namespace APITemplate.Api.Cache;

public sealed class OutputCacheInvalidationService(
    IOutputCacheStore outputCacheStore,
    ILogger<OutputCacheInvalidationService> logger
) : IOutputCacheInvalidationService
{
    public Task EvictAsync(string tag, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return EvictAsync([tag], tenantId, cancellationToken);
    }

    public async Task EvictAsync(
        IEnumerable<string> tags,
        Guid tenantId,
        CancellationToken cancellationToken = default
    )
    {
        string tenantSuffix = tenantId != Guid.Empty ? $"-{tenantId}" : string.Empty;

        foreach (string tag in tags.Distinct(StringComparer.Ordinal))
        {
            string targetTag = $"{tag}{tenantSuffix}";
            try
            {
                await outputCacheStore.EvictByTagAsync(targetTag, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.EvictOutputCacheFailed(ex, targetTag);
            }
        }
    }
}
