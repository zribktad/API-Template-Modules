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
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset CommitDeadlineUtc { get; set; }
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
        DateTimeOffset now = timeProvider.GetUtcNow();
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
        IStoredFileRepository repository,
        ILogger<FileUploadSaga> logger,
        CancellationToken ct
    )
    {
        // Assert the caller's tenant matches the saga's tenant before doing anything. The saga is
        // correlated only by the opaque upload token; without this a token leaked/guessed across
        // tenants could be committed (or its committed file read back) from another tenant's context.
        if (command.TenantId != TenantId)
        {
            logger.LogWarning("CommitUploadCommand for saga {Id} rejected: tenant mismatch", Id);
            return (DomainErrors.Files.FileNotFound(Id ?? "<null>"), null);
        }

        if (Status == FileUploadStatus.Committed && StoredFileId.HasValue)
        {
            logger.LogInformation(
                "CommitUploadCommand redelivered for already-committed saga {Id}",
                Id
            );
            // Runs on the Wolverine worker with no HTTP scope, so the ambient tenant global
            // query filter would evaluate to WHERE false; bypass it and scope by saga state.
            StoredFile? existing = await dbContext
                .StoredFiles.AsNoTracking()
                .IgnoreQueryFilters()
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
                    DateTime.SpecifyKind(existing.Audit.CreatedAtUtc, DateTimeKind.Utc)
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

        // Take the per-(tenant, sha256) advisory lock (shared with MaybeDeleteBlobHandler) before
        // promoting the blob and inserting the referencing row, so an orphan-blob delete cannot run
        // its refcount check between our promote and the row commit and remove a live blob.
        await repository.AcquireBlobDeletionLockAsync(TenantId, Sha256, ct);

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
        // DO NOT call MarkCompleted() here. We keep the saga row until the scheduled TimeoutUploadCommand
        // arrives, providing an idempotency window for redelivered CommitUploadCommand messages.

        UploadCommittedReply reply = new(
            entity.Id,
            entity.OriginalFileName,
            entity.ContentType,
            entity.SizeBytes,
            entity.Description,
            // entity.Audit.CreatedAtUtc is only stamped during SaveChangesAsync (after this handler),
            // so it is still 0001-01-01 here. Use the saga's own creation timestamp instead.
            DateTime.SpecifyKind(CreatedAtUtc.UtcDateTime, DateTimeKind.Utc)
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
        {
            // Clean up the saga row now that the idempotency window has closed.
            MarkCompleted();
            return;
        }

        if (Status != FileUploadStatus.Staged)
        {
            MarkCompleted();
            return;
        }

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
