namespace SharedKernel.Domain.Entities;

/// <summary>
/// Value object that records who created and last modified an entity, and when.
/// Embedded as an owned type on all <see cref="IAuditableEntity"/> implementations.
/// </summary>
public sealed class AuditInfo
{
    public DateTime CreatedAtUtc { get; set; }
    public Guid CreatedBy { get; set; } = AuditDefaults.SystemActorId;
    public DateTime UpdatedAtUtc { get; set; }
    public Guid UpdatedBy { get; set; } = AuditDefaults.SystemActorId;
}
