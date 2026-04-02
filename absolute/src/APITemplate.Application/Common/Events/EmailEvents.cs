namespace APITemplate.Application.Common.Events;

/// <summary>
/// Published after a new user successfully registers, triggering the welcome email notification.
/// </summary>
public sealed record UserRegisteredNotification(Guid UserId, string Email, string Username);

/// <summary>
/// Published after a tenant invitation is created, triggering the invitation email with the acceptance link.
/// </summary>
public sealed record TenantInvitationCreatedNotification(
    Guid InvitationId,
    string Email,
    string TenantName,
    string Token
);

/// <summary>
/// Published after a user's role is changed, triggering the role-change notification email.
/// </summary>
public sealed record UserRoleChangedNotification(
    Guid UserId,
    string Email,
    string Username,
    string OldRole,
    string NewRole
);
