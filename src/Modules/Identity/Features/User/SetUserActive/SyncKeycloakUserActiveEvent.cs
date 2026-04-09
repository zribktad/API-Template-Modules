namespace Identity.Features.User;

public sealed record SyncKeycloakUserActiveEvent(string KeycloakUserId, bool IsActive);
