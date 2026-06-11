namespace APITemplate.Api.Cache;

public interface IOutputCacheInvalidationService
{
    public Task EvictAsync(
        string tag,
        Guid tenantId,
        CancellationToken cancellationToken = default
    );

    public Task EvictAsync(
        IEnumerable<string> tags,
        Guid tenantId,
        CancellationToken cancellationToken = default
    );
}
