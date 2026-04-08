namespace Identity.Features.User;

public sealed class SyncKeycloakUserActiveHandler
{
    public static async Task HandleAsync(
        SyncKeycloakUserActiveEvent @event,
        IKeycloakAdminService keycloakAdmin,
        CancellationToken ct
    )
    {
        await keycloakAdmin.SetUserEnabledAsync(@event.KeycloakUserId, @event.IsActive, ct);
    }
}
