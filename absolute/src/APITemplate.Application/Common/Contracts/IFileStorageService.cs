namespace APITemplate.Application.Common.Contracts;

/// <summary>
/// Application-layer abstraction for binary file storage, decoupling handlers from
/// the concrete storage backend (local disk, blob storage, S3, etc.).
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Persists the contents of <paramref name="fileStream"/> under the given <paramref name="fileName"/>
    /// and returns a <see cref="FileStorageResult"/> containing the resolved storage path and file size.
    /// </summary>
    Task<FileStorageResult> SaveAsync(
        Stream fileStream,
        string fileName,
        CancellationToken ct = default
    );

    /// <summary>
    /// Opens a readable stream for the file at <paramref name="storagePath"/>,
    /// or returns <c>null</c> if the file does not exist.
    /// </summary>
    Task<Stream?> OpenReadAsync(string storagePath, CancellationToken ct = default);

    /// <summary>
    /// Permanently removes the file at <paramref name="storagePath"/> from the storage backend.
    /// </summary>
    Task DeleteAsync(string storagePath, CancellationToken ct = default);
}

/// <summary>
/// Value object returned by <see cref="IFileStorageService.SaveAsync"/> describing where
/// the file was stored and how large it is.
/// </summary>
public sealed record FileStorageResult(string StoragePath, long SizeBytes);
