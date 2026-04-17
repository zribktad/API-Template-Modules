using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

namespace FileStorage.Domain.Services;

/// <summary>
///     Default <see cref="IFileUploadWorkflow" />. Enforces extension whitelist and size limit before delegating
///     to <see cref="IFileStorageService" /> for the physical write, and owns the compensating delete on rollback
///     (executed through the configured retry pipeline).
/// </summary>
internal sealed class FileUploadWorkflow(
    IFileStorageService storage,
    IOptions<FileStorageOptions> options,
    IFileStorageDeletePipelineProvider deletePipelineProvider,
    ILogger<FileUploadWorkflow> logger
) : IFileUploadWorkflow
{
    public async Task<ErrorOr<StoredFile>> PrepareAsync(
        UploadFileRequest request,
        CancellationToken ct = default
    )
    {
        FileStorageOptions opts = options.Value;

        string extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !opts.AllowedExtensions.Contains(extension))
            return DomainErrors.Files.InvalidFileType(
                string.IsNullOrEmpty(extension) ? "none" : extension
            );

        if (request.SizeBytes > opts.MaxFileSizeBytes)
            return DomainErrors.Files.FileTooLarge(opts.MaxFileSizeBytes);

        FileStorageResult result = await storage.SaveAsync(
            request.FileStream,
            request.FileName,
            ct
        );

        return StoredFile.Create(
            request.FileName,
            result.StoragePath,
            request.ContentType,
            result.SizeBytes,
            request.Description
        );
    }

    public async Task RollbackAsync(StoredFile storedFile)
    {
        ResiliencePipeline pipeline = deletePipelineProvider.Get();

        try
        {
            await pipeline.ExecuteAsync(
                async token => await storage.DeleteAsync(storedFile.StoragePath, token),
                CancellationToken.None
            );
        }
        catch (Exception ex)
        {
            // Best-effort: must not throw or the caller's original commit-failure exception is lost.
            // Surface as a warning so orphaned storage payloads are observable after retries are exhausted.
            logger.LogWarning(
                ex,
                "Failed to roll back stored file at {StoragePath} after retries; payload may be orphaned.",
                storedFile.StoragePath
            );
        }
    }
}
