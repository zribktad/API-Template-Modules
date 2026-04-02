using APITemplate.Domain.Enums;

namespace APITemplate.Domain.Entities;

/// <summary>
/// Domain entity representing an email invitation for a user to join a tenant.
/// Holds a hashed token used for secure acceptance and tracks the invitation lifecycle via <see cref="InvitationStatus"/>.
/// </summary>
public sealed class TenantInvitation : IAuditableTenantEntity, IHasId
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string NormalizedEmail { get; set; }
    public required string TokenHash { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

    public Tenant Tenant { get; set; } = null!;

    public Guid TenantId { get; set; }
    public AuditInfo Audit { get; set; } = new();
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedBy { get; set; }
}
