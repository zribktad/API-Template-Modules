using Microsoft.Extensions.Options;
using Notifications.Contracts;
using Notifications.Domain;
using Notifications.Services;
using SharedKernel.Contracts.Events;

namespace Notifications.Features;

public sealed class UserRegisteredEmailHandler
{
    public static async Task HandleAsync(
        UserRegisteredNotification @event,
        IEmailTemplateRenderer templateRenderer,
        IEmailQueue emailQueue,
        IOptions<EmailOptions> options,
        CancellationToken ct
    )
    {
        var html = await templateRenderer.RenderAsync(
            EmailTemplateNames.UserRegistration,
            new
            {
                @event.Username,
                @event.Email,
                LoginUrl = $"{options.Value.BaseUrl}/login",
            },
            ct
        );

        await emailQueue.EnqueueAsync(
            new EmailMessage(
                @event.Email,
                "Welcome to the platform!",
                html,
                EmailTemplateNames.UserRegistration
            ),
            ct
        );
    }
}
