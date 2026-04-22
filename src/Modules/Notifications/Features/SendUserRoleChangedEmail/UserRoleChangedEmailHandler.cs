using Microsoft.Extensions.Logging;
using Notifications.Contracts;
using Notifications.Logging;
using SharedKernel.Application.Errors;
using SharedKernel.Contracts.Events;
using Wolverine;

namespace Notifications.Features;

public sealed class UserRoleChangedEmailHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        UserRoleChangedNotification @event,
        IEmailTemplateRenderer templateRenderer,
        ILogger<UserRoleChangedEmailHandler> logger,
        CancellationToken ct
    )
    {
        ErrorOr<string> html = await templateRenderer.RenderAsync(
            EmailTemplateNames.UserRoleChanged,
            new
            {
                @event.Username,
                @event.OldRole,
                @event.NewRole,
            },
            ct
        );

        if (html.IsError)
        {
            logger.EmailTemplateRenderFailed(
                EmailTemplateNames.UserRoleChanged,
                html.FirstError.Code,
                html.FirstError.Description
            );
            throw new AppException(html.FirstError.Description, html.FirstError.Code);
        }

        OutgoingMessages messages = new();
        messages.Add(
            new EmailMessage(
                @event.Email,
                "Your role has been updated",
                html.Value,
                EmailTemplateNames.UserRoleChanged
            )
        );

        return messages;
    }
}
