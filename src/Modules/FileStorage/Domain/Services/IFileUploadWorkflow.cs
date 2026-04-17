namespace FileStorage.Domain.Services;

/// <summary>
///     Validates an upload request against <see cref="FileStorageOptions" /> (allowed extensions, max size) and
///     persists the binary payload via <see cref="IFileStorageService" />, producing a <see cref="StoredFile" />
///     entity ready for database persistence.
///     <para>
///         The caller (command handler) remains responsible for the DB transaction; on commit failure it MUST
///         invoke <see cref="RollbackAsync" /> so the storage write is removed.
///     </para>
/// </summary>
public interface IFileUploadWorkflow
{
    /// <summary>
    ///     Validates the request and saves the stream to storage. On success returns a not-yet-persisted
    ///     <see cref="StoredFile" />; on validation failure returns a <see cref="DomainErrors.Files" /> error
    ///     and performs no storage writes.
    /// </summary>
    Task<ErrorOr<StoredFile>> PrepareAsync(
        UploadFileRequest request,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Compensating action for a failed DB persist: removes the storage payload created by
    ///     <see cref="PrepareAsync" />. Best-effort — uses <see cref="CancellationToken.None" /> internally so the
    ///     cleanup runs even when the original request is being cancelled.
    /// </summary>
    Task RollbackAsync(StoredFile storedFile);
}
