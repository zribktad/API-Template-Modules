namespace FileStorage.Domain;

/// <summary>
///     Repository contract for <see cref="StoredFile" /> entities, inheriting all generic CRUD operations from
///     <see cref="IRepository{T}" />.
/// </summary>
public interface IStoredFileRepository : IRepository<StoredFile>
{
    /// <summary>
    ///     Acquires a transaction-scoped PostgreSQL advisory lock keyed by <paramref name="tenantId" /> and
    ///     <paramref name="sha256" />. Used to serialise the orphan-blob refcount-check-then-delete against the
    ///     concurrent promote-then-insert of a deduplicated upload, so a still-referenced blob is never removed.
    ///     Must be called inside an open transaction; the lock releases automatically on commit/rollback.
    /// </summary>
    public Task AcquireBlobDeletionLockAsync(
        Guid tenantId,
        string sha256,
        CancellationToken ct = default
    );
}
