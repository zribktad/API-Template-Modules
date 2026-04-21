using FileStorage.Domain.Storage;

namespace FileStorage.Features.Download;

public sealed record DownloadFileQuery(DownloadFileRequest Request);

public sealed record FileDownloadResult(Stream FileStream, string ContentType, string FileName);

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
        Stream? stream = await store.OpenReadAsync(entity.TenantId, entity.Sha256, ct);
        if (stream is null)
            return DomainErrors.Files.FileNotFound(entity.OriginalFileName);

        return new FileDownloadResult(stream, entity.ContentType, entity.OriginalFileName);
    }
}
