using System.ComponentModel.DataAnnotations;
using FileStorage.Domain.Sagas;
using Microsoft.Extensions.Options;
using Wolverine;

namespace FileStorage.Features.Commit;

/// <summary>HTTP body for <c>POST /files/commit</c>.</summary>
public sealed record CommitUploadRequest(
    [property: Required, MinLength(1)] string UploadToken,
    [property: Required, MinLength(1), MaxLength(100)] string ContentType,
    [property: MaxLength(500)] string? Description
);

/// <summary>Wolverine command that forwards a commit request to the saga and awaits its reply.</summary>
public sealed record CommitUploadEndpointCommand(CommitUploadRequest Request);

/// <summary>
///     Bridges the HTTP commit endpoint to the saga: validates the client-supplied ContentType against
///     <see cref="FileStorageOptions.AllowedContentTypes" /> (stored-XSS defence), invokes the saga's
///     <see cref="CommitUploadCommand" /> synchronously, and translates the reply to a
///     <see cref="FileUploadResponse" />.
/// </summary>
public sealed class CommitUploadEndpointCommandHandler
{
    public static async Task<ErrorOr<FileUploadResponse>> HandleAsync(
        CommitUploadEndpointCommand command,
        IOptions<FileStorageOptions> options,
        IMessageBus bus,
        CancellationToken ct
    )
    {
        FileStorageOptions opts = options.Value;
        string contentType = command.Request.ContentType.Trim();

        if (!opts.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            return DomainErrors.Files.InvalidFileType(contentType);

        CommitUploadCommand sagaMessage = new(
            command.Request.UploadToken,
            contentType,
            command.Request.Description
        );

        ErrorOr<UploadCommittedReply> reply = await bus.InvokeAsync<ErrorOr<UploadCommittedReply>>(
            sagaMessage,
            ct
        );

        if (reply.IsError)
            return reply.Errors;

        UploadCommittedReply r = reply.Value;
        return new FileUploadResponse(
            r.StoredFileId,
            r.OriginalFileName,
            r.ContentType,
            r.SizeBytes,
            r.Description,
            r.CreatedAtUtc
        );
    }
}
