using APITemplate.Api;
using BuildingBlocks.Application.Context;
using Microsoft.AspNetCore.OutputCaching;

namespace APITemplate.Api.Cache;

public sealed class OutputCacheInvalidationService(
    IOutputCacheStore outputCacheStore,
    ITenantProvider tenantProvider,
    ILogger<OutputCacheInvalidationService> logger
) : IOutputCacheInvalidationService
{
    public Task EvictAsync(string tag, CancellationToken cancellationToken = default)
    {
        return EvictAsync([tag], cancellationToken);
    }

    public async Task EvictAsync(
        IEnumerable<string> tags,
        CancellationToken cancellationToken = default
    )
    {
        string tenantSuffix = tenantProvider.HasTenant
            ? $"-{tenantProvider.TenantId}"
            : string.Empty;

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
