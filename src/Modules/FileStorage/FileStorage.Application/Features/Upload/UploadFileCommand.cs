using SharedKernel.Application.Contracts;
using SharedKernel.Application.Errors;
using SharedKernel.Application.Options.Infrastructure;
using FileStorage.Application.Features.DTOs;
using FileStorage.Domain;
using SharedKernel.Domain.Interfaces;
using ErrorOr;
using Microsoft.Extensions.Options;

namespace FileStorage.Application.Features.Upload;

public sealed record UploadFileCommand(UploadFileRequest Request);

public sealed class UploadFileCommandHandler
{
    public static async Task<ErrorOr<FileUploadResponse>> HandleAsync(
        UploadFileCommand command,
        IStoredFileRepository repository,
        IFileStorageService storage,
        IUnitOfWork unitOfWork,
        IOptions<FileStorageOptions> options,
        CancellationToken ct
    )
    {
        var req = command.Request;
        var opts = options.Value;
        var extension = Path.GetExtension(req.FileName)?.ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !opts.AllowedExtensions.Contains(extension))
            return DomainErrors.Examples.InvalidFileType(extension ?? "none");

        if (req.SizeBytes > opts.MaxFileSizeBytes)
            return DomainErrors.Examples.FileTooLarge(opts.MaxFileSizeBytes);

        var storageResult = await storage.SaveAsync(req.FileStream, req.FileName, ct);

        try
        {
            var entity = new StoredFile
            {
                Id = Guid.NewGuid(),
                OriginalFileName = req.FileName,
                StoragePath = storageResult.StoragePath,
                ContentType = req.ContentType,
                SizeBytes = storageResult.SizeBytes,
                Description = req.Description,
            };

            await unitOfWork.ExecuteInTransactionAsync(
                async () =>
                {
                    await repository.AddAsync(entity, ct);
                },
                ct
            );

            return new FileUploadResponse(
                entity.Id,
                entity.OriginalFileName,
                entity.ContentType,
                entity.SizeBytes,
                entity.Description,
                entity.Audit.CreatedAtUtc
            );
        }
        catch
        {
            await storage.DeleteAsync(storageResult.StoragePath, CancellationToken.None);
            throw;
        }
    }
}
