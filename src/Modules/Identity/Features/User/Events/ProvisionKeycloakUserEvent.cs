namespace Identity.Features.User.Events;

/// <summary>
///     Published after an <see cref="Entities.AppUser" /> is persisted to the database.
///     Wolverine durable outbox delivers this event to <c>ProvisionKeycloakUserHandler</c>,
///     which creates the corresponding Keycloak account and links it back to the user record.
/// </summary>
public sealed record ProvisionKeycloakUserEvent(Guid UserId, string Username, string Email);
