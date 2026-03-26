namespace APITemplate.Api.Cache;

/// <summary>
/// Abstraction for evicting output cache entries by tag, allowing the infrastructure
/// implementation to be swapped or mocked independently of the domain/application layers.
/// </summary>
public interface IOutputCacheInvalidationService
{
    /// <summary>Evicts all output cache entries associated with <paramref name="tag"/>.</summary>
    Task EvictAsync(string tag, CancellationToken cancellationToken = default);

    /// <summary>Evicts all output cache entries associated with each of the provided <paramref name="tags"/>.</summary>
    Task EvictAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default);
}
