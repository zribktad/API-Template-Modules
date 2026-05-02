using Asp.Versioning;
using FileStorage.Features.Commit;
using FileStorage.Features.Delete;
using FileStorage.Features.Download;
using FileStorage.Features.Staging;
using FileStorage.Features.Upload;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Wolverine;

namespace FileStorage.Features;

/// <summary>
///     Presentation-layer controller for the content-addressed file storage module. Supports a legacy v1
///     single-step upload (facade) and the new two-phase staging+commit flow backed by
///     <see cref="FileStorage.Domain.Sagas.FileUploadSaga" />.
/// </summary>
[ApiVersion(1.0)]
public sealed class FilesController(IMessageBus bus) : ApiControllerBase
{
    /// <summary>
    ///     Legacy single-step upload. Internally chains staging + commit so old clients keep working;
    ///     new clients should prefer <see cref="BeginUpload" /> + <see cref="CommitUpload" />.
    /// </summary>
    [HttpPost("upload")]
    [RequirePermission(Permission.Examples.Upload)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<FileUploadResponse>> Upload(
        [FromForm] FileUploadRequest request,
        CancellationToken ct
    )
    {
        await using Stream stream = request.File.OpenReadStream();
        ErrorOr<FileUploadResponse> result = await bus.InvokeAsync<ErrorOr<FileUploadResponse>>(
            new UploadFileCommand(
                new UploadFileRequest(
                    stream,
                    request.File.FileName,
                    request.File.ContentType,
                    request.File.Length,
                    request.Description
                )
            ),
            ct
        );
        if (result.IsError)
            return result.ToActionResult(this);

        return CreatedAtAction(
            nameof(Download),
            new { id = result.Value.Id, version = this.GetApiVersion() },
            result.Value
        );
    }

    /// <summary>
    ///     Two-phase upload, phase 1: streams the file to the staging area and returns an opaque
    ///     <c>uploadToken</c> plus the server-computed SHA-256 and size. No <see cref="StoredFile" /> row is
    ///     created yet — call <see cref="CommitUpload" /> within <c>StagingTtlMinutes</c> to finalise.
    /// </summary>
    [HttpPost("staging")]
    [RequirePermission(Permission.Examples.Upload)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<BeginUploadResponse>> BeginUpload(
        [FromForm] FileUploadRequest request,
        CancellationToken ct
    )
    {
        await using Stream stream = request.File.OpenReadStream();
        ErrorOr<BeginUploadResponse> result = await bus.InvokeAsync<ErrorOr<BeginUploadResponse>>(
            new BeginUploadEndpointCommand(
                new BeginUploadRequest(stream, request.File.FileName, request.File.Length)
            ),
            ct
        );
        return result.ToActionResult(this);
    }

    /// <summary>
    ///     Two-phase upload, phase 2: commits a previously staged payload, producing the final
    ///     <see cref="StoredFile" /> row and cascading <c>StoredFileCreatedNotification</c>.
    /// </summary>
    [HttpPost("commit")]
    [RequirePermission(Permission.Examples.Upload)]
    public async Task<ActionResult<FileUploadResponse>> CommitUpload(
        [FromBody] CommitUploadRequest request,
        CancellationToken ct
    )
    {
        ErrorOr<FileUploadResponse> result = await bus.InvokeAsync<ErrorOr<FileUploadResponse>>(
            new CommitUploadEndpointCommand(request),
            ct
        );
        if (result.IsError)
            return result.ToActionResult(this);

        return CreatedAtAction(
            nameof(Download),
            new { id = result.Value.Id, version = this.GetApiVersion() },
            result.Value
        );
    }

    /// <summary>
    ///     Streams the committed blob to the caller. The response always carries
    ///     <c>Content-Disposition: attachment</c> and <c>X-Content-Type-Options: nosniff</c> so a tenant
    ///     cannot smuggle HTML/JS by lying about the content type at upload time.
    /// </summary>
    [HttpGet("{id:guid}/download")]
    [RequirePermission(Permission.Examples.Download)]
    public async Task<IActionResult> Download(
        [FromRoute] DownloadFileRequest request,
        CancellationToken ct
    )
    {
        ErrorOr<FileDownloadResult> result = await bus.InvokeAsync<ErrorOr<FileDownloadResult>>(
            new DownloadFileQuery(request),
            ct
        );
        if (result.IsError)
            return result.ToErrorResult(this);

        try
        {
            FileStreamResult fileResult = File(
                result.Value.FileStream,
                result.Value.ContentType,
                result.Value.FileName,
                enableRangeProcessing: true
            );

            fileResult.EntityTag = EntityTagHeaderValue.Parse($"\"{result.Value.Sha256}\"");
            fileResult.LastModified = result.Value.CreatedAtUtc;

            return fileResult;
        }
        catch
        {
            await result.Value.FileStream.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    ///     Soft-deletes the <see cref="StoredFile" /> and cascades a refcount check for the backing blob
    ///     via <c>MaybeDeleteBlobCommand</c>. The physical blob is removed only when no active references
    ///     remain within the tenant.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.Examples.Upload)]
    public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken ct)
    {
        ErrorOr<Success> result = await bus.InvokeAsync<ErrorOr<Success>>(
            new DeleteFileCommand(id),
            ct
        );
        return result.ToNoContentResult(this);
    }
}
