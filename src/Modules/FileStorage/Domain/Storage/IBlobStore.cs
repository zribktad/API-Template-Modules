namespace FileStorage.Domain.Storage;

/// <summary>
///     Abstraction over a content-addressed blob store. Implementations write bytes to a staging area, compute
///     the SHA-256 digest, and on commit promote the staging payload to a per-tenant content-addressed path.
///     <para>
///         Multi-backend seam: each implementation is selected at runtime by <c>BackendKey</c> via
///         <see cref="IBlobStoreFactory" />. Domain code never assumes a particular backend.
///     </para>
/// </summary>
public interface IBlobStore
{
    /// <summary>
    ///     Streams <paramref name="content" /> into a staging location while computing its SHA-256 digest.
    ///     The returned <see cref="StagingResult" /> can be promoted via <see cref="PromoteToCommittedAsync" />
    ///     or discarded via <see cref="DeleteStagingAsync" />.
    ///     Returns <see cref="ErrorOr{T}" /> with <see cref="DomainErrors.Files.FileTooLarge" /> if the payload
    ///     exceeds the configured limit.
    /// </summary>
    Task<ErrorOr<StagingResult>> WriteStagingAsync(Stream content, CancellationToken ct = default);

    /// <summary>
    ///     Atomically moves a staging payload to its content-addressed committed location for the given
    ///     <paramref name="tenantId" /> / <paramref name="sha256" />. Idempotent: if the committed path already
    ///     exists and its size matches <paramref name="expectedSize" />, the staging copy is removed and the
    ///     existing committed path is returned. Returns <see cref="ErrorOr{T}" /> with
    ///     <see cref="DomainErrors.Files" /> errors on conflict or path traversal.
    /// </summary>
    Task<ErrorOr<string>> PromoteToCommittedAsync(
        Guid tenantId,
        string sha256,
        long expectedSize,
        string stagingPath,
        CancellationToken ct = default
    );

    /// <summary>
    ///     Opens the committed blob for reading. Returns <see langword="null" /> if it does not exist.
    /// </summary>
    Task<Stream?> OpenReadAsync(Guid tenantId, string sha256, CancellationToken ct = default);

    /// <summary>
    ///     Deletes the committed blob. Silent success if it does not exist (idempotent).
    ///     IOException after retries is logged internally and not surfaced.
    /// </summary>
    Task DeleteAsync(Guid tenantId, string sha256, CancellationToken ct = default);

    /// <summary>
    ///     Deletes a staging payload. Silent success if it does not exist (idempotent).
    /// </summary>
    Task DeleteStagingAsync(string stagingPath, CancellationToken ct = default);
}
