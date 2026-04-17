using FileStorage.Domain.Sagas;
using FileStorage.Domain.Storage;
using Microsoft.Extensions.Options;
using SharedKernel.Application.Context;
using Wolverine;

namespace FileStorage.Features.Staging;

/// <summary>HTTP payload for the new two-phase upload's staging call.</summary>
public sealed record BeginUploadRequest(Stream FileStream, string FileName, long SizeBytes);

/// <summary>
///     Response from <c>POST /files/staging</c>: carries the opaque <see cref="UploadToken" /> the client must
///     present to <c>POST /files/commit</c>, plus the server-computed content hash and size.
/// </summary>
public sealed record BeginUploadResponse(string UploadToken, string Sha256, long SizeBytes);

/// <summary>Wolverine command wrapping a staging upload.</summary>
public sealed record BeginUploadEndpointCommand(BeginUploadRequest Request);

/// <summary>
///     Handler for <see cref="BeginUploadEndpointCommand" />. Validates the request, streams the payload to
///     the blob store's staging area while computing SHA-256, awaits saga creation via
///     <c>bus.InvokeAsync(BeginUploadCommand)</c>, and schedules the <see cref="TimeoutUploadCommand" /> via
///     <c>bus.ScheduleAsync</c> so the staging payload is reaped if the client never commits.
/// </summary>
public sealed class BeginUploadEndpointCommandHandler
{
    public static async Task<ErrorOr<BeginUploadResponse>> HandleAsync(
        BeginUploadEndpointCommand command,
        IBlobStoreFactory blobStoreFactory,
        IOptions<FileStorageOptions> options,
        ITenantProvider tenantProvider,
        IMessageBus bus,
        CancellationToken ct
    )
    {
        FileStorageOptions opts = options.Value;
        BeginUploadRequest request = command.Request;

        if (string.IsNullOrWhiteSpace(request.FileName))
            return DomainErrors.Files.InvalidFileType("none");

        string extension = (Path.GetExtension(request.FileName) ?? string.Empty).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !opts.AllowedExtensions.Contains(extension))
            return DomainErrors.Files.InvalidFileType(
                string.IsNullOrEmpty(extension) ? "none" : extension
            );

        // Client-declared size is trusted only as a fast-fail; LocalBlobStore.WriteStagingAsync enforces
        // MaxFileSizeBytes during the stream read to prevent disk-fill DoS from a lying client.
        if (request.SizeBytes > opts.MaxFileSizeBytes)
            return DomainErrors.Files.FileTooLarge(opts.MaxFileSizeBytes);

        IBlobStore store = blobStoreFactory.Get(opts.BackendKey);
        StagingResult staging;
        try
        {
            staging = await store.WriteStagingAsync(request.FileStream, ct);
        }
        catch (FileTooLargeException)
        {
            return DomainErrors.Files.FileTooLarge(opts.MaxFileSizeBytes);
        }

        string uploadToken = Guid.NewGuid().ToString("N");

        BeginUploadCommand sagaStart = new(
            uploadToken,
            tenantProvider.TenantId,
            staging.Sha256,
            staging.SizeBytes,
            request.FileName,
            staging.StagingPath,
            opts.BackendKey
        );

        // InvokeAsync (not PublishAsync) awaits saga creation before returning — avoids a race where the
        // client receives a token before the saga row is persisted, then posts /commit and hits "not found".
        await bus.InvokeAsync(sagaStart, ct);

        // Schedule the timeout explicitly; returning it from Saga.Start as a cascading message would
        // dispatch immediately with no delay.
        await bus.ScheduleAsync(
            new TimeoutUploadCommand(uploadToken),
            TimeSpan.FromMinutes(opts.StagingTtlMinutes)
        );

        return new BeginUploadResponse(uploadToken, staging.Sha256, staging.SizeBytes);
    }
}

/// <summary>Thrown by <see cref="IBlobStore" /> implementations when the streamed payload exceeds the limit.</summary>
public sealed class FileTooLargeException : Exception
{
    public FileTooLargeException(long maxBytes)
        : base($"Upload exceeded the maximum allowed size of {maxBytes} bytes.") { }
}
