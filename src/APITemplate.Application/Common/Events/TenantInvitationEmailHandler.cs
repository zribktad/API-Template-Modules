using APITemplate.Application.Common.Email;
using APITemplate.Application.Common.Options;
using Microsoft.Extensions.Options;

namespace APITemplate.Application.Common.Events;

public sealed class TenantInvitationEmailHandler
{
    public static async Task HandleAsync(
        TenantInvitationCreatedNotification @event,
        IEmailTemplateRenderer templateRenderer,
        IEmailQueue emailQueue,
        IOptions<EmailOptions> options,
        CancellationToken ct
    )
    {
        var html = await templateRenderer.RenderAsync(
            EmailTemplateNames.TenantInvitation,
            new
            {
                @event.Email,
                @event.TenantName,
                InvitationUrl = $"{options.Value.BaseUrl}/invitations/accept?token={@event.Token}",
                ExpiryHours = options.Value.InvitationTokenExpiryHours,
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
