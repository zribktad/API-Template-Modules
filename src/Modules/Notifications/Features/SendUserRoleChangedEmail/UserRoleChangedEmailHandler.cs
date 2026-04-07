using Notifications.Contracts;
using SharedKernel.Contracts.Events;
using Wolverine;

namespace Notifications.Features;

public sealed class UserRoleChangedEmailHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        UserRoleChangedNotification @event,
        IEmailTemplateRenderer templateRenderer,
        CancellationToken ct
    )
    {
        string html = await templateRenderer.RenderAsync(
            EmailTemplateNames.UserRoleChanged,
            new
            {
                @event.Username,
                @event.OldRole,
                @event.NewRole,
            },
            ct
        );

        OutgoingMessages messages = new();
        messages.Add(
            new EmailMessage(
                @event.Email,
                "Your role has been updated",
                html,
                EmailTemplateNames.UserRoleChanged
            )
        );

        return messages;
    }
}
