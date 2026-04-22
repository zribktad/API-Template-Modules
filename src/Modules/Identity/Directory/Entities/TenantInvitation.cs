using ErrorOr;
using Identity.ValueObjects;

namespace Identity.Directory.Entities;

/// <summary>
///     Domain entity representing an email invitation for a user to join a tenant.
///     Holds a hashed token used for secure acceptance and tracks the invitation lifecycle via
///     <see cref="InvitationStatus" />.
/// </summary>
public sealed class TenantInvitation : IAuditableTenantEntity, IHasId
{
    public Email Email
    {
        get => field;
        private set
        {
            field = value;
            NormalizedEmail = value.Normalize();
        }
    }

    public string NormalizedEmail { get; private set; } = string.Empty;
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; private set; }
    public InvitationStatus Status { get; private set; } = InvitationStatus.Pending;

    public Guid TenantId { get; set; }
    public AuditInfo Audit { get; set; } = new();
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedBy { get; set; }
    public Guid Id { get; set; }

    public static TenantInvitation Create(
        Email email,
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

    public bool IsExpired(TimeProvider timeProvider)
    {
        return ExpiresAtUtc < timeProvider.GetUtcNow().UtcDateTime;
    }

    public ErrorOr<Success> Accept(TimeProvider timeProvider)
    {
        if (IsExpired(timeProvider))
            return IdentityDomainErrors.Invitations.Expired();
        if (Status == InvitationStatus.Accepted)
            return IdentityDomainErrors.Invitations.AlreadyAccepted();
        Status = InvitationStatus.Accepted;
        return Result.Success;
    }

    public void Revoke()
    {
        Status = InvitationStatus.Revoked;
    }

    /// <summary>
    ///     Guards the resend preconditions: invitation must be <see cref="InvitationStatus.Pending" /> and not yet expired.
    ///     Returns an error if either condition is violated; otherwise returns success.
    /// </summary>
    public ErrorOr<Success> TryResend(TimeProvider timeProvider)
    {
        if (Status != InvitationStatus.Pending)
            return IdentityDomainErrors.Invitations.NotPending();

        if (IsExpired(timeProvider))
            return IdentityDomainErrors.Invitations.ExpiredCreateNew();

        return Result.Success;
    }

    public void RefreshToken(string tokenHash)
    {
        TokenHash = tokenHash;
    }
}
