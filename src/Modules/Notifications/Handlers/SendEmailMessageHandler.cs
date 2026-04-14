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

        try
        {
            await pipeline.ExecuteAsync(
                async token =>
                {
                    await sender.SendAsync(message, token);
                },
                ct
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.EmailSendFailed(ex, message.To, message.Subject);
            await failedEmailStore.StoreFailedAsync(message, ex.Message, ct);
        }
    }
}
