using System.Buffers;
using System.Security.Cryptography;
using FileStorage.Domain.Services;
using FileStorage.Domain.Storage;
using FileStorage.Features.Staging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using SharedKernel.Application.Errors;
using FS = FileStorage.Domain.ErrorCatalog;

namespace FileStorage.Services;

/// <summary>
///     Local-filesystem <see cref="IBlobStore" />. Streams writes to a staging file while computing SHA-256,
///     atomically promotes the staging file to the content-addressed path
///     <c>{BlobsPath}/{tenantId}/{sha[:2]}/{sha}</c>, and runs delete through the shared Polly retry pipeline
///     so transient I/O failures do not leak orphan payloads.
/// </summary>
internal sealed class LocalBlobStore : IBlobStore
{
    private const int CopyBufferSize = 81920;

    private readonly FileStorageOptions _options;
    private readonly IFileStorageDeletePipelineProvider _deletePipelineProvider;
    private readonly ILogger<LocalBlobStore> _logger;

    public LocalBlobStore(
        IOptions<FileStorageOptions> options,
        IFileStorageDeletePipelineProvider deletePipelineProvider,
        ILogger<LocalBlobStore> logger
    )
    {
        _options = options.Value;
        _deletePipelineProvider = deletePipelineProvider;
        _logger = logger;
    }

    public async Task<StagingResult> WriteStagingAsync(
        Stream content,
        CancellationToken ct = default
    )
    {
        string stagingDir = _options.ResolveStagingPath();
        Directory.CreateDirectory(stagingDir);
        string stagingPath = Path.Combine(stagingDir, Guid.NewGuid().ToString("N"));

        ValidatePathWithin(stagingDir, stagingPath);

        long maxBytes = _options.MaxFileSizeBytes;
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long sizeBytes = 0;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(CopyBufferSize);

        try
        {
            await using (
                FileStream output = new(
                    stagingPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    CopyBufferSize,
                    FileOptions.Asynchronous
                )
            )
            {
                int read;
                while ((read = await content.ReadAsync(buffer.AsMemory(0, CopyBufferSize), ct)) > 0)
                {
                    sizeBytes += read;
                    if (sizeBytes > maxBytes)
                    {
                        await output.DisposeAsync();
                        File.Delete(stagingPath);
                        throw new FileTooLargeException(maxBytes);
                    }

                    hasher.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), ct);
                }
            }
        }
        catch
        {
            try
            {
                File.Delete(stagingPath);
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            catch (IOException) { }
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        string sha256 = Convert.ToHexStringLower(hasher.GetHashAndReset());
        return new StagingResult(stagingPath, sha256, sizeBytes);
    }

    public async Task<string> PromoteToCommittedAsync(
        Guid tenantId,
        string sha256,
        long expectedSize,
        string stagingPath,
        CancellationToken ct = default
    )
    {
        string blobsRoot = _options.ResolveBlobsPath();
        string committedPath = BuildCommittedPath(blobsRoot, tenantId, sha256);

        ValidatePathWithin(blobsRoot, committedPath);
        ValidatePathWithin(_options.ResolveStagingPath(), stagingPath);

        Directory.CreateDirectory(Path.GetDirectoryName(committedPath)!);

        FileInfo committedInfo = new(committedPath);
        if (committedInfo.Exists)
        {
            if (committedInfo.Length != expectedSize)
                throw new AppException(
                    $"Blob {sha256} already exists for tenant {tenantId} with size {committedInfo.Length}, "
                        + $"but staging expects {expectedSize}. Refusing to overwrite.",
                    FS.Files.BlobConflict
                );

            await DeleteStagingAsync(stagingPath, ct);
            return committedPath;
        }

        try
        {
            File.Move(stagingPath, committedPath);
        }
        catch (IOException) when (File.Exists(committedPath))
        {
            long existingSize = new FileInfo(committedPath).Length;
            if (existingSize != expectedSize)
                throw new AppException(
                    $"Concurrent promote produced size mismatch for blob {sha256}.",
                    FS.Files.BlobConflict
                );
            await DeleteStagingAsync(stagingPath, ct);
        }

        return committedPath;
    }

    public Task<Stream?> OpenReadAsync(Guid tenantId, string sha256, CancellationToken ct = default)
    {
        string blobsRoot = _options.ResolveBlobsPath();
        string committedPath = BuildCommittedPath(blobsRoot, tenantId, sha256);
        ValidatePathWithin(blobsRoot, committedPath);

        if (!File.Exists(committedPath))
            return Task.FromResult<Stream?>(null);

        return Task.FromResult<Stream?>(
            new FileStream(
                committedPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                CopyBufferSize,
                FileOptions.Asynchronous
            )
        );
    }

    public async Task DeleteAsync(Guid tenantId, string sha256, CancellationToken ct = default)
    {
        string blobsRoot = _options.ResolveBlobsPath();
        string committedPath = BuildCommittedPath(blobsRoot, tenantId, sha256);
        ValidatePathWithin(blobsRoot, committedPath);

        ResiliencePipeline pipeline = _deletePipelineProvider.Get();
        try
        {
            await pipeline.ExecuteAsync(
                _ =>
                {
                    try
                    {
                        File.Delete(committedPath);
                    }
                    catch (FileNotFoundException) { }
                    catch (DirectoryNotFoundException) { }
                    return ValueTask.CompletedTask;
                },
                ct
            );
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to delete blob {Sha256} for tenant {TenantId} after retries; may be orphaned.",
                sha256,
                tenantId
            );
        }
    }

    public Task DeleteStagingAsync(string stagingPath, CancellationToken ct = default)
    {
        ValidatePathWithin(_options.ResolveStagingPath(), stagingPath);

        try
        {
            File.Delete(stagingPath);
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }

        return Task.CompletedTask;
    }

    private static string BuildCommittedPath(string blobsRoot, Guid tenantId, string sha256)
    {
        if (sha256.Length < 2)
            throw new ArgumentException("SHA-256 digest must be 64 hex chars.", nameof(sha256));

        string prefix = sha256[..2];
        return Path.Combine(blobsRoot, tenantId.ToString(), prefix, sha256);
    }

    /// <summary>
    ///     Throws <see cref="UnauthorizedAccessException" /> if <paramref name="path" /> resolves outside
    ///     <paramref name="root" />, preventing path-traversal attacks.
    /// </summary>
    private static void ValidatePathWithin(string root, string path)
    {
        string fullPath = Path.GetFullPath(path);
        string fullRoot =
            Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new AppException(
                "Path traversal detected: access denied.",
                FS.Files.PathTraversal
            );
    }
}
