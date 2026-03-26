using APITemplate.Api.Authorization;
using APITemplate.Api.Controllers;
using APITemplate.Api.ErrorOrMapping;
using APITemplate.Api.Requests;
using APITemplate.Application.Common.Security;
using APITemplate.Application.Features.Examples;
using APITemplate.Application.Features.Examples.DTOs;
using Asp.Versioning;
using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace APITemplate.Api.Controllers.V1;

[ApiVersion(1.0)]
/// <summary>
/// Presentation-layer controller that demonstrates multipart file upload and streamed download
/// using local file storage, limited to 10 MB per upload request.
/// </summary>
public sealed class FilesController(IMessageBus bus) : ApiControllerBase
{
    /// <summary>
    /// Accepts a multipart form upload, streams the file to local storage via the application
    /// layer, and returns a 201 with a Location header pointing to the download endpoint.
    /// </summary>
    [HttpPost("upload")]
    [RequirePermission(Permission.Examples.Upload)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<FileUploadResponse>> Upload(
        [FromForm] FileUploadRequest request,
        CancellationToken ct
    )
    {
        await using var stream = request.File.OpenReadStream();
        var result = await bus.InvokeAsync<ErrorOr<FileUploadResponse>>(
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
    /// Streams the stored file back to the caller, disposing the underlying stream on error to
    /// prevent resource leaks.
    /// </summary>
    [HttpGet("{id:guid}/download")]
    [RequirePermission(Permission.Examples.Download)]
    public async Task<IActionResult> Download(
        [FromRoute] DownloadFileRequest request,
        CancellationToken ct
    )
    {
        var result = await bus.InvokeAsync<ErrorOr<FileDownloadResult>>(
            new DownloadFileQuery(request),
            ct
        );
        if (result.IsError)
            return result.ToErrorResult(this);

        try
        {
            return File(result.Value.FileStream, result.Value.ContentType, result.Value.FileName);
        }
        catch
        {
            await result.Value.FileStream.DisposeAsync();
            throw;
        }
    }
}
