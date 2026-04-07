using Microsoft.Extensions.Options;
using Notifications.Contracts;
using SharedKernel.Contracts.Events;
using Wolverine;

namespace Notifications.Features;

public sealed class UserRegisteredEmailHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        UserRegisteredNotification @event,
        IEmailTemplateRenderer templateRenderer,
        IOptions<EmailOptions> options,
        CancellationToken ct
    )
    {
        string html = await templateRenderer.RenderAsync(
            EmailTemplateNames.UserRegistration,
            new
            {
                @event.Username,
                @event.Email,
                LoginUrl = $"{options.Value.BaseUrl}/login",
            },
            ct
        );

        OutgoingMessages messages = new();
        messages.Add(
            new EmailMessage(
                @event.Email,
                "Welcome to the platform!",
                html,
                EmailTemplateNames.UserRegistration
            )
        );

        return messages;
    }
}
