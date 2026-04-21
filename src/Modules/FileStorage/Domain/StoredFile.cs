using SharedKernel.Domain.Entities;
using SharedKernel.Domain.Entities.Contracts;

namespace FileStorage.Domain;

/// <summary>
///     Domain entity representing metadata for a file uploaded to blob storage.
///     <para>
///         Content is stored externally in a content-addressed blob store; this row points at the blob by
///         <see cref="Sha256" /> + <see cref="BackendKey" /> and is the refcount anchor — the blob is deleted
///         when no non-soft-deleted <see cref="StoredFile" /> row references it within the tenant.
///     </para>
/// </summary>
public sealed class StoredFile : IAuditableTenantEntity, IHasId
{
    public required string OriginalFileName { get; init; }

    /// <summary>Lower-case hex SHA-256 digest of file bytes (64 chars).</summary>
    public required string Sha256 { get; init; }

    /// <summary>Identifies the blob-store backend that physically holds the bytes (e.g. "local", future "s3").</summary>
    public required string BackendKey { get; init; }

    public required string ContentType { get; init; }
    public long SizeBytes { get; init; }
    public string? Description { get; init; }

    public static StoredFile Create(
        string originalFileName,
        string sha256,
        string backendKey,
        string contentType,
        long sizeBytes,
        string? description
    )
    {
        return new StoredFile
        {
            Id = Guid.NewGuid(),
            OriginalFileName = originalFileName,
            Sha256 = sha256,
            BackendKey = backendKey,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            Description = description,
        };
    }

    public Guid TenantId { get; set; }
    public AuditInfo Audit { get; set; } = new();
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedBy { get; set; }
    public Guid Id { get; set; }
}
