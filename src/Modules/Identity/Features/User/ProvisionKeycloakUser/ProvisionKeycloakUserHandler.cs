using Identity.Logging;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Identity.Features.User;

/// <summary>
///     Wolverine handler delivered via the durable outbox that provisions a Keycloak account for a
///     newly created <see cref="Entities.AppUser" /> and links it back to the local record.
///     Idempotent: silently skips if the user no longer exists or is already linked to Keycloak.
/// </summary>
public sealed class ProvisionKeycloakUserHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        ProvisionKeycloakUserEvent @event,
        IUserRepository repository,
        IUnitOfWork<IdentityDbMarker> unitOfWork,
        IKeycloakAdminService keycloakAdmin,
        ILogger<ProvisionKeycloakUserHandler> logger,
        CancellationToken ct
    )
    {
        AppUser? user = await repository.GetByIdAsync(@event.UserId, ct);

        if (user is null || user.KeycloakUserId is not null)
        {
            logger.ProvisionKeycloakUserSkipped(@event.UserId);
            return OutgoingMessagesHelper.Empty;
        }

        string keycloakUserId = await keycloakAdmin.CreateUserAsync(
            @event.Username,
            @event.Email,
            ct
        );

        user.LinkKeycloak(keycloakUserId);
        await repository.UpdateAsync(user, ct);
        await unitOfWork.CommitAsync(ct);

        logger.KeycloakUserProvisioned(user.Id, keycloakUserId);

        OutgoingMessages messages = new();
        messages.Add(new UserRegisteredNotification(user.Id, @event.Email, @event.Username));
        return messages;
    }
}
