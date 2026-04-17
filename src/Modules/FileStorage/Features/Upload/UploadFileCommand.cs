using FileStorage.Domain.Services;

namespace FileStorage.Features.Upload;

public sealed record UploadFileCommand(UploadFileRequest Request);

public sealed class UploadFileCommandHandler
{
    public static async Task<ErrorOr<FileUploadResponse>> HandleAsync(
        UploadFileCommand command,
        IFileUploadWorkflow workflow,
        IStoredFileRepository repository,
        IUnitOfWork<FileStorageDbMarker> unitOfWork,
        CancellationToken ct
    )
    {
        ErrorOr<StoredFile> prepared = await workflow.PrepareAsync(command.Request, ct);
        if (prepared.IsError)
            return prepared.Errors;
        StoredFile entity = prepared.Value;

        try
        {
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
            await workflow.RollbackAsync(entity);
            throw;
        }
    }
}
