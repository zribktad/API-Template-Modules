using Microsoft.Extensions.Logging;
using Notifications.Contracts;
using Notifications.Logging;
using Polly;

namespace Notifications.Handlers;

public sealed class SendEmailMessageHandler
{
    public static async Task HandleAsync(
        EmailMessage message,
        IEmailSender sender,
        ISmtpSendPipelineProvider smtpSendPipelineProvider,
        IFailedEmailStore failedEmailStore,
        ILogger<SendEmailMessageHandler> logger,
        CancellationToken ct
    )
    {
        ResiliencePipeline pipeline = smtpSendPipelineProvider.Get();

        ErrorOr<Success> result = await pipeline.ExecuteAsync(
            async token => await sender.SendAsync(message, token),
            ct
        );

        if (result.IsError)
        {
            logger.EmailSendFailedWithError(
                message.To,
                message.Subject,
                result.FirstError.Code,
                result.FirstError.Description
            );
            await failedEmailStore.StoreFailedAsync(message, result.FirstError.Description, ct);
        }
    }
}
