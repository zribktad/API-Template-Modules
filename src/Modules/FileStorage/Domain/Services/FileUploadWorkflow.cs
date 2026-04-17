using Microsoft.Extensions.Options;

namespace FileStorage.Domain.Services;

/// <summary>
///     Default <see cref="IFileUploadWorkflow" />. Enforces extension whitelist and size limit before delegating
///     to <see cref="IFileStorageService" /> for the physical write.
/// </summary>
internal sealed class FileUploadWorkflow(
    IFileStorageService storage,
    IOptions<FileStorageOptions> options
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
}
