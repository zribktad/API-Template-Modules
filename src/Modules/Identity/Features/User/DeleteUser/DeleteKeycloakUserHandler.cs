namespace Identity.Features.User;

public sealed class DeleteKeycloakUserHandler
{
    public static async Task HandleAsync(
        DeleteKeycloakUserEvent @event,
        IKeycloakAdminService keycloakAdmin,
        CancellationToken ct
    )
    {
        await keycloakAdmin.DeleteUserAsync(@event.KeycloakUserId, ct);
    }
}
