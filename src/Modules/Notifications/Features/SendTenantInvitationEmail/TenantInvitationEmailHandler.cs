using Microsoft.Extensions.Logging;
using Notifications.Contracts;
using SharedKernel.Contracts.Events;
using Wolverine;

namespace Notifications.Features;

public sealed class TenantInvitationEmailHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        TenantInvitationCreatedNotification @event,
        IEmailTemplateRenderer templateRenderer,
        ILogger<TenantInvitationEmailHandler> logger,
        CancellationToken ct
    )
    {
        ErrorOr<string> html = await templateRenderer.RenderAsync(
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

        EmailHandlerHelper.ThrowIfRenderFailed(html, EmailTemplateNames.TenantInvitation, logger);

        OutgoingMessages messages = new();
        messages.Add(
            new EmailMessage(
                @event.Email,
                $"You've been invited to {@event.TenantName}",
                html.Value,
                EmailTemplateNames.TenantInvitation,
                Retryable: true
            )
        );

        return messages;
    }
}
