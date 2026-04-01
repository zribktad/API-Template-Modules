namespace FileStorage.Application.Features.Download;

public sealed record DownloadFileQuery(DownloadFileRequest Request);

public sealed record FileDownloadResult(Stream FileStream, string ContentType, string FileName);

public sealed class DownloadFileQueryHandler
{
    public static async Task<ErrorOr<FileDownloadResult>> HandleAsync(
        DownloadFileQuery query,
        IStoredFileRepository repository,
        IFileStorageService storage,
        CancellationToken ct
    )
    {
        StoredFile? entity = await repository.GetByIdAsync(query.Request.Id, ct);
        if (entity is null)
            return DomainErrors.Files.FileNotFound(query.Request.Id.ToString());

        Stream? stream = await storage.OpenReadAsync(entity.StoragePath, ct);
        if (stream is null)
            return DomainErrors.Files.FileNotFound(entity.OriginalFileName);

        return new FileDownloadResult(stream, entity.ContentType, entity.OriginalFileName);
    }
}
