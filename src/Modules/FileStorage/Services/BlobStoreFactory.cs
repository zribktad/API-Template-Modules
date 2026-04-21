using FileStorage.Domain.Storage;

namespace FileStorage.Services;

/// <summary>
///     Default <see cref="IBlobStoreFactory" />. Resolves the implementation by key via a map populated at
///     DI registration time. Unknown keys throw so corrupted <c>BackendKey</c> values fail loudly.
/// </summary>
internal sealed class BlobStoreFactory : IBlobStoreFactory
{
    private readonly IReadOnlyDictionary<string, IBlobStore> _stores;

    public BlobStoreFactory(IEnumerable<KeyedBlobStore> stores)
    {
        _stores = stores.ToDictionary(s => s.Key, s => s.Store, StringComparer.OrdinalIgnoreCase);
    }

    public IBlobStore Get(string backendKey)
    {
        if (string.IsNullOrWhiteSpace(backendKey))
            throw new ArgumentException(
                "Backend key must not be null or empty.",
                nameof(backendKey)
            );

        if (!_stores.TryGetValue(backendKey, out IBlobStore? store))
            throw new InvalidOperationException(
                $"No blob store registered for backend key '{backendKey}'. "
                    + $"Known keys: {string.Join(", ", _stores.Keys)}."
            );

        return store;
    }
}

/// <summary>DI tuple — pairs a backend key with its <see cref="IBlobStore" /> implementation.</summary>
public sealed record KeyedBlobStore(string Key, IBlobStore Store);
