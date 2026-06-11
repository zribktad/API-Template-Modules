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
                token => new ValueTask(sender.SendAsync(message, token)),
                ct
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.EmailSendFailed(ex, message.To, message.Subject);
            // Persist the failed email with CancellationToken.None: this is must-complete recovery work
            // (the send already failed). Using the message token would let a shutdown abort the store and
            // silently lose a retryable email after Wolverine has acked the handler.
            await failedEmailStore.StoreFailedAsync(message, ex.Message, CancellationToken.None);
        }
    }
}
