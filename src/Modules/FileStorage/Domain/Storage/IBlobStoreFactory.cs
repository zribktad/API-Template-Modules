namespace FileStorage.Domain.Storage;

/// <summary>
///     Resolves an <see cref="IBlobStore" /> implementation by backend key (e.g. "local", future "s3").
///     Throws on unknown keys — never silently falls back, so corrupted <c>BackendKey</c> values surface
///     as explicit errors.
/// </summary>
public interface IBlobStoreFactory
{
    IBlobStore Get(string backendKey);
}
