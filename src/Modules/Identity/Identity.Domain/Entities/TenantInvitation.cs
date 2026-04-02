using ErrorOr;

namespace Identity.Domain.Entities;

/// <summary>
/// Domain entity representing an email invitation for a user to join a tenant.
/// Holds a hashed token used for secure acceptance and tracks the invitation lifecycle via <see cref="InvitationStatus"/>.
/// </summary>
public sealed class TenantInvitation : IAuditableTenantEntity, IHasId
{
    public Guid Id { get; set; }

    public string Email
    {
        get => field;
        private set
        {
            field = string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("Invitation email cannot be empty.", nameof(Email))
                : value.Trim();
            NormalizedEmail = AppUser.NormalizeEmail(field);
        }
    } = string.Empty;

    public string NormalizedEmail { get; private set; } = string.Empty;
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; private set; }
    public InvitationStatus Status { get; private set; } = InvitationStatus.Pending;

    public Guid TenantId { get; set; }
    public AuditInfo Audit { get; set; } = new();
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedBy { get; set; }

    public static TenantInvitation Create(
        string email,
        string tokenHash,
        int expiryHours,
        TimeProvider timeProvider
    )
    {
        TenantInvitation invitation = new();
        invitation.Id = Guid.NewGuid();
        invitation.Email = email;
        invitation.TokenHash = tokenHash;
        invitation.ExpiresAtUtc = timeProvider.GetUtcNow().UtcDateTime.AddHours(expiryHours);
        return invitation;
    }

    public bool IsExpired(TimeProvider timeProvider) =>
        ExpiresAtUtc < timeProvider.GetUtcNow().UtcDateTime;

    public ErrorOr<Success> Accept(TimeProvider timeProvider)
    {
        if (IsExpired(timeProvider))
            return Error.Conflict("INV-0410", "Invitation has expired.");
        if (Status == InvitationStatus.Accepted)
            return Error.Conflict("INV-0409-ACCEPTED", "Invitation has already been accepted.");
        Status = InvitationStatus.Accepted;
        return Result.Success;
    }

    public void Revoke() => Status = InvitationStatus.Revoked;

    public void RefreshToken(string tokenHash) => TokenHash = tokenHash;
}
