using Microsoft.AspNetCore.OutputCaching;

namespace APITemplate.Api.Cache;

public sealed class OutputCacheInvalidationService(IOutputCacheStore outputCacheStore)
    : IOutputCacheInvalidationService
{
    public Task EvictAsync(string tag, CancellationToken cancellationToken = default) =>
        EvictAsync([tag], cancellationToken);

    public async Task EvictAsync(
        IEnumerable<string> tags,
        CancellationToken cancellationToken = default
    )
    {
        foreach (string tag in tags.Distinct(StringComparer.Ordinal))
        {
            try
            {
                await outputCacheStore.EvictByTagAsync(tag, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                continue;
            }
        }
    }
}
