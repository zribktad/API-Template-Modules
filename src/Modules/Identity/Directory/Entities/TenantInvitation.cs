using System.ComponentModel.DataAnnotations.Schema;
using ErrorOr;
using SharedKernel.Infrastructure.Logging;

namespace Identity.Directory.Entities;

/// <summary>
///     Email invitation for a user to join a tenant. The token is stored hashed (never plain-text);
///     acceptance is gated on expiry and current status to prevent replay and double-accept.
/// </summary>
public sealed class TenantInvitation : IAuditableTenantEntity, IHasId
{
    public const int EmailMaxLength = 320;

    internal string DbEmail { get; private set; } = null!;
    internal string DbNormalizedEmail { get; private set; } = null!;

    [NotMapped]
    [PersonalData]
    public NormalizedString Email
    {
        get => new NormalizedString(DbEmail);
        set
        {
            DbEmail = value.Value;
            DbNormalizedEmail = value.Normalized;
        }
    }

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
        string email,
        string tokenHash,
        int expiryHours,
        TimeProvider timeProvider
    )
    {
        TenantInvitation invitation = new();
        invitation.Id = Guid.NewGuid();
        invitation.Email = new NormalizedString(email);
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
    ///     Validates that the invitation can be resent. A resend is only allowed while the invitation is
    ///     still <see cref="InvitationStatus.Pending"/> and not yet expired — once accepted, revoked, or
    ///     expired a new invitation must be created instead.
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
