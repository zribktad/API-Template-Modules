using Microsoft.Extensions.Options;
using Notifications.Shared;
using SharedKernel.Contracts.Events;

namespace Notifications.Features;

public sealed class UserRoleChangedEmailHandler
{
    public static async Task HandleAsync(
        UserRoleChangedNotification @event,
        IEmailTemplateRenderer templateRenderer,
        IEmailQueue emailQueue,
        CancellationToken ct
    )
    {
        var html = await templateRenderer.RenderAsync(
            EmailTemplateNames.UserRoleChanged,
            new
            {
                @event.Username,
                @event.OldRole,
                @event.NewRole,
            },
            ct
        );

        await emailQueue.EnqueueAsync(
            new EmailMessage(
                @event.Email,
                "Your role has been updated",
                html,
                EmailTemplateNames.UserRoleChanged
            ),
            ct
        );
    }
}



