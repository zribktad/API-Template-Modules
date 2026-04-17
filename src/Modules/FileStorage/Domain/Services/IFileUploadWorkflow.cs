namespace FileStorage.Domain.Services;

/// <summary>
///     Validates an upload request against <see cref="FileStorageOptions" /> (allowed extensions, max size) and
///     persists the binary payload via <see cref="IFileStorageService" />, producing a <see cref="StoredFile" />
///     entity ready for database persistence.
///     <para>
///         The caller (command handler) remains responsible for the DB transaction and compensating storage
///         cleanup on commit failure — the workflow owns only pre-DB steps.
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
}
