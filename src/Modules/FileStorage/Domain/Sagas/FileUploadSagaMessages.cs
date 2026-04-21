namespace FileStorage.Domain.Sagas;

/// <summary>Kicks off the <see cref="FileUploadSaga" /> after bytes land in the staging area.</summary>
public sealed record BeginUploadCommand(
    string UploadToken,
    Guid TenantId,
    string Sha256,
    long SizeBytes,
    string OriginalFileName,
    string StagingPath,
    string BackendKey
);

/// <summary>Finalises a staged upload: promotes the blob and inserts the <see cref="StoredFile" /> row.</summary>
public sealed record CommitUploadCommand(
    string UploadToken,
    string ContentType,
    string? Description
);

/// <summary>Scheduled timeout message; fires after the configured staging TTL if no commit arrived.</summary>
public sealed record TimeoutUploadCommand(string UploadToken);

/// <summary>
///     Cascading message emitted by the delete flow — triggers a refcount check and blob removal when
///     no non-soft-deleted <see cref="StoredFile" /> row references the given hash within the tenant.
/// </summary>
public sealed record MaybeDeleteBlobCommand(Guid TenantId, string Sha256, string BackendKey);

/// <summary>Event published after a successful commit; downstream modules can react (audit, search, cache).</summary>
public sealed record StoredFileCreatedNotification(
    Guid StoredFileId,
    Guid TenantId,
    string Sha256,
    string BackendKey,
    string OriginalFileName,
    string ContentType,
    long SizeBytes
);

/// <summary>
///     Result of a successful commit, returned synchronously to the commit-endpoint caller so it can
///     respond with the finalised <see cref="StoredFile" /> id.
/// </summary>
public sealed record UploadCommittedReply(
    Guid StoredFileId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string? Description,
    DateTime CreatedAtUtc
);
