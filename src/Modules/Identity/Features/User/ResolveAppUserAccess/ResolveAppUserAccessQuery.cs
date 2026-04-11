namespace Identity.Features.User;

public sealed record ResolveAppUserAccessQuery(
    string KeycloakUserId,
    string Email,
    string Username
);
