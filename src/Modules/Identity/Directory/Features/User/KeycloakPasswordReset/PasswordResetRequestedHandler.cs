namespace Identity.Directory.Features.User;

public sealed class PasswordResetRequestedHandler
{
    public static async Task HandleAsync(
        PasswordResetRequestedEvent @event,
        IKeycloakAdminService keycloakAdmin,
        CancellationToken ct
    )
    {
        await keycloakAdmin.SendPasswordResetEmailAsync(@event.KeycloakUserId, ct);
    }
}
