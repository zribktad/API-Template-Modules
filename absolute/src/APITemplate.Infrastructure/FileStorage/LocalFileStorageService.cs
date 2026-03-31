using APITemplate.Application.Common.Context;
using APITemplate.Application.Common.Contracts;
using APITemplate.Application.Common.Options;
using Microsoft.Extensions.Options;

namespace APITemplate.Infrastructure.FileStorage;

/// <summary>
/// Infrastructure implementation of <see cref="IFileStorageService"/> that persists files to the
/// local file system under a tenant-scoped subdirectory within the configured base path.
/// All path operations include path-traversal validation to prevent directory escape attacks.
/// </summary>
public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly FileStorageOptions _options;
    private readonly ITenantProvider _tenantProvider;

    public LocalFileStorageService(
        IOptions<FileStorageOptions> options,
        ITenantProvider tenantProvider
    )
    {
        _options = options.Value;
        _tenantProvider = tenantProvider;
    }

    /// <summary>
    /// Saves <paramref name="fileStream"/> to the tenant directory using a UUID-based file name
    /// that retains the original extension, validates the resolved path, and returns the storage path and size.
    /// </summary>
    public async Task<FileStorageResult> SaveAsync(
        Stream fileStream,
        string fileName,
        CancellationToken ct = default
    )
    {
        var tenantDir = Path.Combine(_options.BasePath, _tenantProvider.TenantId.ToString());
        Directory.CreateDirectory(tenantDir);

        var safeExtension = Path.GetExtension(Path.GetFileName(fileName));
        var storedFileName = $"{Guid.NewGuid()}{safeExtension}";
        var storagePath = Path.Combine(tenantDir, storedFileName);

        ValidatePathWithinBasePath(storagePath);

        long sizeBytes;
        await using (
            var output = new FileStream(
                storagePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous
            )
        )
        {
            await fileStream.CopyToAsync(output, ct);
            sizeBytes = output.Length;
        }

        return new FileStorageResult(storagePath, sizeBytes);
    }

    /// <summary>Opens the file at <paramref name="storagePath"/> for reading after path validation; returns <see langword="null"/> if the file does not exist.</summary>
    public Task<Stream?> OpenReadAsync(string storagePath, CancellationToken ct = default)
    {
        ValidatePathWithinBasePath(storagePath);

        if (!File.Exists(storagePath))
            return Task.FromResult<Stream?>(null);

        return Task.FromResult<Stream?>(
            new FileStream(
                storagePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous
            )
        );
    }

    /// <summary>Deletes the file at <paramref name="storagePath"/> after path validation; silently succeeds if the file does not exist.</summary>
    public Task DeleteAsync(string storagePath, CancellationToken ct = default)
    {
        ValidatePathWithinBasePath(storagePath);

        if (File.Exists(storagePath))
            File.Delete(storagePath);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Throws <see cref="UnauthorizedAccessException"/> if the fully resolved <paramref name="path"/>
    /// does not reside within the configured base path, preventing path-traversal attacks.
    /// </summary>
    private void ValidatePathWithinBasePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var fullBasePath =
            Path.GetFullPath(_options.BasePath).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(fullBasePath, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Path traversal detected: access denied.");
    }
}
