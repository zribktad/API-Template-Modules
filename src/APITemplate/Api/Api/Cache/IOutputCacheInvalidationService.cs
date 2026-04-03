namespace APITemplate.Api.Cache;

public interface IOutputCacheInvalidationService
{
    Task EvictAsync(string tag, CancellationToken cancellationToken = default);

    Task EvictAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default);
}
