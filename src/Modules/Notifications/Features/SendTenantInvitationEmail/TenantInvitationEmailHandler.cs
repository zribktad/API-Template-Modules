using Notifications.Contracts;
using SharedKernel.Contracts.Events;

namespace Notifications.Features;

public sealed class TenantInvitationEmailHandler
{
    public static async Task HandleAsync(
        TenantInvitationCreatedNotification @event,
        IEmailTemplateRenderer templateRenderer,
        IEmailQueue emailQueue,
        CancellationToken ct
    )
    {
        string html = await templateRenderer.RenderAsync(
            EmailTemplateNames.TenantInvitation,
            new
            {
                @event.Email,
                @event.TenantName,
                @event.InvitationUrl,
                @event.ExpiryHours,
            },
            ct
        );

        await emailQueue.EnqueueAsync(
            new EmailMessage(
                @event.Email,
                $"You've been invited to {@event.TenantName}",
                html,
                EmailTemplateNames.TenantInvitation,
                Retryable: true
            ),
            ct
        );
    }
}
