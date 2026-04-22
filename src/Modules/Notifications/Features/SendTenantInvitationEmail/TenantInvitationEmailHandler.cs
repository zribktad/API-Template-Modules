using Notifications.Contracts;
using SharedKernel.Contracts.Events;
using Wolverine;

namespace Notifications.Features;

public sealed class TenantInvitationEmailHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        TenantInvitationCreatedNotification @event,
        IEmailTemplateRenderer templateRenderer,
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

        OutgoingMessages messages = new();
        messages.Add(
            new EmailMessage(
                @event.Email,
                $"You've been invited to {@event.TenantName}",
                html,
                EmailTemplateNames.TenantInvitation,
                Retryable: true
            )
        );

        return messages;
    }
}
