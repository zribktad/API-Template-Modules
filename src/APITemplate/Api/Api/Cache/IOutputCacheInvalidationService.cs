namespace APITemplate.Api.Cache;

public interface IOutputCacheInvalidationService
{
    public Task EvictAsync(string tag, CancellationToken cancellationToken = default);

    public Task EvictAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default);
}
