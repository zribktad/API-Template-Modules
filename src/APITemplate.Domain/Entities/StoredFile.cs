namespace APITemplate.Domain.Entities;

/// <summary>
/// Domain entity representing metadata for a file uploaded to blob storage.
/// The actual binary content is stored externally; this entity tracks the reference and descriptive metadata.
/// </summary>
public sealed class StoredFile : IAuditableTenantEntity, IHasId
{
    public Guid Id { get; set; }
    public required string OriginalFileName { get; init; }
    public required string StoragePath { get; init; }
    public required string ContentType { get; init; }
    public long SizeBytes { get; init; }
    public string? Description { get; init; }
    public Guid TenantId { get; set; }
    public AuditInfo Audit { get; set; } = new();
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedBy { get; set; }
}
