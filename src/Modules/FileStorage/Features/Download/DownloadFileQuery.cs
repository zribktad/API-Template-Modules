using FileStorage.Contracts;
using FileStorage.Domain.Storage;

namespace FileStorage.Features.Download;

public sealed record DownloadFileQuery(DownloadFileRequest Request);

public sealed class DownloadFileQueryHandler
{
    public static async Task<ErrorOr<FileDownloadResult>> HandleAsync(
        DownloadFileQuery query,
        IStoredFileRepository repository,
        IBlobStoreFactory blobStoreFactory,
        CancellationToken ct
    )
    {
        StoredFile? entity = await repository.GetByIdAsync(query.Request.Id, ct);
        if (entity is null)
            return DomainErrors.Files.FileNotFound(query.Request.Id.ToString());

        IBlobStore store = blobStoreFactory.Get(entity.BackendKey);
        ErrorOr<Stream> openResult = await store.OpenReadAsync(entity.TenantId, entity.Sha256, ct);
        if (openResult.IsError)
        {
            return openResult.FirstError.Type == ErrorType.NotFound
                ? DomainErrors.Files.FileNotFound(entity.OriginalFileName)
                : openResult.Errors;
        }

        return new FileDownloadResult(
            openResult.Value,
            entity.ContentType,
            entity.OriginalFileName,
            entity.Sha256,
            entity.Audit.CreatedAtUtc
        );
    }
}
