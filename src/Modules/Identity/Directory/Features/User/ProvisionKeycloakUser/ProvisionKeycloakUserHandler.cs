using ErrorOr;
using Identity.Logging;
using Microsoft.Extensions.Logging;
using SharedKernel.Application.Errors;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace Identity.Directory.Features.User;

/// <summary>
///     Wolverine handler delivered via the durable outbox that provisions a Keycloak account for a
///     newly created <see cref="Entities.AppUser" /> and links it back to the local record.
///     Idempotent: silently skips if the user no longer exists or is already linked to Keycloak.
/// </summary>
public sealed class ProvisionKeycloakUserHandler
{
    /// <summary>
    ///     Overrides the global <c>HttpRequestException</c> retry policy for this handler
    ///     to log a structured alert before dead-lettering. The retry schedule intentionally
    ///     mirrors the global one — Wolverine handler-level rules take full precedence,
    ///     so the global <c>MoveToErrorQueue</c> action would never fire for this handler.
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
                        ILogger retryLogger =
                            runtime.LoggerFactory.CreateLogger<ProvisionKeycloakUserHandler>();
                        retryLogger.KeycloakProvisioningPermanentlyFailed(message.UserId);
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

        ErrorOr<string> createResult = await keycloakAdmin.CreateUserAsync(
            @event.Username,
            @event.Email,
            ct
        );

        if (createResult.IsError)
        {
            Error err = createResult.FirstError;
            if (err.Type == ErrorType.Failure)
                // Transient HTTP-level failure — rethrow as HttpRequestException so Wolverine's
                // retry chain (5s / 30s / 5min) applies before dead-lettering.
                throw new HttpRequestException(err.Description);

            // Permanent conflict/validation error — dead-letter immediately with structured log.
            logger.KeycloakProvisioningPermanentlyFailed(user.Id);
            throw new AppException(err.Description, err.Code);
        }

        string keycloakUserId = createResult.Value;
        user.LinkKeycloak(keycloakUserId);
        await repository.UpdateAsync(user, ct);
        await unitOfWork.CommitAsync(ct);

        logger.KeycloakUserProvisioned(user.Id, keycloakUserId);

        OutgoingMessages messages = new();
        messages.Add(new UserRegisteredNotification(user.Id, @event.Email, @event.Username));
        return messages;
    }
}
