using FileStorage.Features.Commit;
using FileStorage.Features.Staging;
using Wolverine;

namespace FileStorage.Features.Upload;

/// <summary>Legacy single-step upload. Kept as a v1-compatible facade that chains staging + commit.</summary>
public sealed record UploadFileCommand(UploadFileRequest Request);

/// <summary>
///     Facade handler preserving the v1 API: synchronously stages the bytes, starts the saga, and commits,
///     returning the same <see cref="FileUploadResponse" /> the original single-step endpoint produced.
///     <para>
///         Clients needing resumable/large uploads should migrate to <c>POST /files/staging</c> +
///         <c>POST /files/commit</c>.
///     </para>
/// </summary>
public sealed class UploadFileCommandHandler
{
    public static async Task<ErrorOr<FileUploadResponse>> HandleAsync(
        UploadFileCommand command,
        IMessageBus bus,
        CancellationToken ct
    )
    {
        UploadFileRequest request = command.Request;

        ErrorOr<BeginUploadResponse> staged = await bus.InvokeAsync<ErrorOr<BeginUploadResponse>>(
            new BeginUploadEndpointCommand(
                new BeginUploadRequest(request.FileStream, request.FileName, request.SizeBytes)
            ),
            ct
        );
        if (staged.IsError)
            return staged.Errors;

        return await bus.InvokeAsync<ErrorOr<FileUploadResponse>>(
            new CommitUploadEndpointCommand(
                new CommitUploadRequest(
                    staged.Value.UploadToken,
                    request.ContentType,
                    request.Description
                )
            ),
            ct
        );
    }
}
