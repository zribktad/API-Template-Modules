namespace APITemplate.Domain.Enums;

/// <summary>
/// Represents the lifecycle state of a <see cref="Entities.TenantInvitation"/>.
/// </summary>
public enum InvitationStatus
{
    /// <summary>The invitation has been sent and is awaiting a response.</summary>
    Pending = 0,

    /// <summary>The invitee accepted the invitation and joined the tenant.</summary>
    Accepted = 1,

    /// <summary>The invitation passed its expiry date without being accepted.</summary>
    Expired = 2,

    /// <summary>The invitation was revoked by a tenant administrator before it could be accepted.</summary>
    Revoked = 3,
}
