namespace Notifications.Contracts;

/// <summary>
/// Central registry of email template identifiers used by <see cref="IEmailTemplateRenderer"/>.
/// Centralising these strings prevents magic-string duplication across notification handlers.
/// </summary>
public static class EmailTemplateNames
{
    public const string UserRegistration = "Features.SendUserRegisteredEmail.user-registration";
    public const string TenantInvitation = "Features.SendTenantInvitationEmail.tenant-invitation";
    public const string UserRoleChanged = "Features.SendUserRoleChangedEmail.user-role-changed";
}
