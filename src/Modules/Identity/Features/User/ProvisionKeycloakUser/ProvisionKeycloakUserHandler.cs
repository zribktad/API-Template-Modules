using Identity.Logging;
using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace Identity.Features.User;

/// <summary>
///     Wolverine handler delivered via the durable outbox that provisions a Keycloak account for a
///     newly created <see cref="Entities.AppUser" /> and links it back to the local record.
///     Idempotent: silently skips if the user no longer exists or is already linked to Keycloak.
/// </summary>
public sealed class ProvisionKeycloakUserHandler
{
    /// <summary>
    ///     Wolverine convention: configures handler-specific error handling.
    ///     After the global retry policy exhausts all attempts, logs a structured alert
    ///     before the message moves to the dead-letter queue.
    /// </summary>
    public static void Configure(HandlerChain chain)
    {
        chain
            .OnException<HttpRequestException>()
            .ScheduleRetry(
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromMinutes(5)
            )
            .Then.CustomAction(
                async (runtime, lifecycle, exception) =>
                {
                    if (lifecycle.Envelope?.Message is ProvisionKeycloakUserEvent message)
                    {
                        ILogger logger =
                            runtime.LoggerFactory.CreateLogger<ProvisionKeycloakUserHandler>();
                        logger.KeycloakProvisioningPermanentlyFailed(message.UserId);
                    }

                    await lifecycle.MoveToDeadLetterQueueAsync(exception);
                },
                "Log permanent provisioning failure before dead-lettering"
            );
    }

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
        user.ProvisioningStatus = ProvisioningStatus.Completed;
        await repository.UpdateAsync(user, ct);
        await unitOfWork.CommitAsync(ct);

        logger.KeycloakUserProvisioned(user.Id, keycloakUserId);

        OutgoingMessages messages = new();
        messages.Add(new UserRegisteredNotification(user.Id, @event.Email, @event.Username));
        return messages;
    }
}
