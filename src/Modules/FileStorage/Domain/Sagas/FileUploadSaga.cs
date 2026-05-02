using FileStorage.Domain.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine;

namespace FileStorage.Domain.Sagas;

/// <summary>
///     Lifecycle of a two-phase file upload.
///     <para>
///         <c>(missing) → Staged → Committed</c> (happy path)<br />
///         <c>Staged → Failed</c> via timeout (client never called commit)
///     </para>
/// </summary>
public enum FileUploadStatus
{
    Staged = 0,
    Committed = 1,
    Failed = 2,
}

/// <summary>
///     Wolverine EF saga that orchestrates the staging → commit handshake for file uploads.
///     <para>
///         <c>Start(BeginUploadCommand)</c> persists the saga row, records staging metadata, and schedules
///         a <see cref="TimeoutUploadCommand" /> by <c>StagingTtlMinutes</c>.
///     </para>
///     <para>
///         <c>Handle(CommitUploadCommand)</c> promotes the staging payload to the content-addressed path,
///         creates the <see cref="StoredFile" /> row, returns a reply to the caller, and emits
///         <see cref="StoredFileCreatedNotification" /> via the transactional outbox.
///     </para>
///     <para>
///         <c>Handle(TimeoutUploadCommand)</c> compensates by deleting the staging payload and marking the
///         saga as <see cref="FileUploadStatus.Failed" />. No-op if the saga is already
///         <see cref="FileUploadStatus.Committed" /> (timeout lost the race to commit).
///     </para>
/// </summary>
public sealed class FileUploadSaga : Saga
{
    /// <summary>Upload token (a Guid rendered as "N") — doubles as the saga correlation key.</summary>
    public string? Id { get; set; }

    public Guid TenantId { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StagingPath { get; set; } = string.Empty;
    public string BackendKey { get; set; } = string.Empty;
    public FileUploadStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime CommitDeadlineUtc { get; set; }
    public Guid? StoredFileId { get; set; }

    /// <summary>
    ///     Creates the saga row. The staging-timeout message is scheduled separately by
    ///     <c>BeginUploadEndpointCommandHandler</c> via <c>IMessageBus.ScheduleAsync</c>; a cascading
    ///     return would be dispatched immediately with no delay.
    /// </summary>
    public static FileUploadSaga Start(
        BeginUploadCommand command,
        IOptions<FileStorageOptions> options,
        TimeProvider timeProvider
    )
    {
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        int ttlMinutes = options.Value.StagingTtlMinutes;

        return new FileUploadSaga
        {
            Id = command.Id,
            TenantId = command.TenantId,
            Sha256 = command.Sha256,
            SizeBytes = command.SizeBytes,
            OriginalFileName = command.OriginalFileName,
            StagingPath = command.StagingPath,
            BackendKey = command.BackendKey,
            Status = FileUploadStatus.Staged,
            CreatedAtUtc = now,
            CommitDeadlineUtc = now.AddMinutes(ttlMinutes),
        };
    }

    public async Task<(ErrorOr<UploadCommittedReply>, StoredFileCreatedNotification?)> Handle(
        CommitUploadCommand command,
        IBlobStoreFactory blobStoreFactory,
        FileStorageDbContext dbContext,
        ILogger<FileUploadSaga> logger,
        CancellationToken ct
    )
    {
        if (Status == FileUploadStatus.Committed && StoredFileId.HasValue)
        {
            logger.LogInformation(
                "CommitUploadCommand redelivered for already-committed saga {Id}",
                Id
            );
            StoredFile? existing = await dbContext
                .StoredFiles.AsNoTracking()
                .FirstOrDefaultAsync(
                    f => f.Id == StoredFileId!.Value && f.TenantId == TenantId,
                    ct
                );
            if (existing is null)
                return (DomainErrors.Files.FileNotFound(StoredFileId.ToString()!), null);

            return (
                new UploadCommittedReply(
                    existing.Id,
                    existing.OriginalFileName,
                    existing.ContentType,
                    existing.SizeBytes,
                    existing.Description,
                    existing.Audit.CreatedAtUtc
                ),
                null
            );
        }

        if (Status != FileUploadStatus.Staged)
        {
            logger.LogWarning(
                "CommitUploadCommand received for saga {Id} in terminal status {Status}; ignoring",
                Id,
                Status
            );
            return (DomainErrors.Files.CommitAfterTerminalState(Id ?? "<null>"), null);
        }

        IBlobStore store = blobStoreFactory.Get(BackendKey);
        ErrorOr<string> promoted = await store.PromoteToCommittedAsync(
            TenantId,
            Sha256,
            SizeBytes,
            StagingPath,
            ct
        );
        if (promoted.IsError)
            return (promoted.Errors, null);

        StoredFile entity = StoredFile.Create(
            OriginalFileName,
            Sha256,
            BackendKey,
            command.ContentType,
            SizeBytes,
            command.Description
        );
        // Ambient ITenantProvider may not be populated on the Wolverine worker; stamp from saga state.
        entity.TenantId = TenantId;
        dbContext.StoredFiles.Add(entity);

        StoredFileId = entity.Id;
        Status = FileUploadStatus.Committed;
        MarkCompleted();

        UploadCommittedReply reply = new(
            entity.Id,
            entity.OriginalFileName,
            entity.ContentType,
            entity.SizeBytes,
            entity.Description,
            entity.Audit.CreatedAtUtc
        );

        StoredFileCreatedNotification notification = new(
            entity.Id,
            TenantId,
            Sha256,
            BackendKey,
            OriginalFileName,
            command.ContentType,
            SizeBytes
        );

        return (reply, notification);
    }

    public async Task Handle(
        TimeoutUploadCommand command,
        IBlobStoreFactory blobStoreFactory,
        ILogger<FileUploadSaga> logger,
        CancellationToken ct
    )
    {
        if (Status == FileUploadStatus.Committed)
            return;

        if (Status != FileUploadStatus.Staged)
            return;

        logger.LogInformation(
            "Upload saga {Id} timed out without commit; deleting staging payload at {StagingPath}",
            Id,
            StagingPath
        );

        IBlobStore store = blobStoreFactory.Get(BackendKey);
        await store.DeleteStagingAsync(StagingPath, ct);

        Status = FileUploadStatus.Failed;
        MarkCompleted();
    }
}
