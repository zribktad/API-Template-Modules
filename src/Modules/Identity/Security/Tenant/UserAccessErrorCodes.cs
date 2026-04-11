namespace Identity.Security.Tenant;

/// <summary>Stable machine-readable codes for application access denial during token validation.</summary>
public static class UserAccessErrorCodes
{
    public const string MissingTenantClaim = "identity.access.missing_tenant_claim";
    public const string MissingProfileClaims = "identity.access.missing_profile_claims";
    public const string PendingInvitation = "identity.access.pending_invitation";
    public const string InvitationExpired = "identity.access.invitation_expired";
    public const string InvitationRevoked = "identity.access.invitation_revoked";
    public const string NoInvitation = "identity.access.no_invitation";
}
