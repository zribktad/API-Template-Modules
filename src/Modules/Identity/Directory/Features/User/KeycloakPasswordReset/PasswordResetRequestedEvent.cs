namespace Identity.Directory.Features.User;

public sealed record PasswordResetRequestedEvent(string KeycloakUserId);
