using APITemplate.Application.Common.Contracts;
using APITemplate.Application.Common.Errors;
using APITemplate.Application.Common.Extensions;
using APITemplate.Application.Features.Examples.DTOs;
using APITemplate.Domain.Interfaces;
using ErrorOr;

namespace APITemplate.Application.Features.Examples;

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
        var entityResult = await repository.GetByIdOrError(
            query.Request.Id,
            DomainErrors.Examples.FileNotFound(query.Request.Id.ToString()),
            ct
        );
        if (entityResult.IsError)
            return entityResult.Errors;
        var entity = entityResult.Value;

        var stream = await storage.OpenReadAsync(entity.StoragePath, ct);
        if (stream is null)
            return DomainErrors.Examples.FileNotFound(entity.OriginalFileName);

        return new FileDownloadResult(stream, entity.ContentType, entity.OriginalFileName);
    }
}
