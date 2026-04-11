namespace Identity.Security.Tenant;

/// <summary>English user-facing messages for access denial (API ProblemDetails / BFF redirect).</summary>
public static class UserAccessDeniedMessages
{
    public const string MissingTenantClaim =
        "The token is missing a valid tenant (tenant_id). Contact your administrator.";

    public const string MissingProfileClaims =
        "The token is missing required account information (sub, email, or username). Contact your administrator.";

    public const string PendingInvitation =
        "Please accept your organization invitation (link in email), then sign in again.";

    public const string InvitationExpired =
        "This invitation has expired. Ask an administrator for a new invitation.";

    public const string InvitationRevoked =
        "This invitation was revoked. Contact your administrator.";

    public const string NoInvitation =
        "Your account is not authorized for this application. Contact your administrator.";
}
