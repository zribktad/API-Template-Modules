namespace Identity.Features.User;

public sealed record PasswordResetRequestedEvent(string KeycloakUserId);
