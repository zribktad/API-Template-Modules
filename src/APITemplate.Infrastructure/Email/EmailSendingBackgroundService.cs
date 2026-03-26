using APITemplate.Application.Common.Email;
using APITemplate.Application.Common.Resilience;
using APITemplate.Infrastructure.BackgroundJobs.Services;
using Microsoft.Extensions.Logging;
using Polly.Registry;

namespace APITemplate.Infrastructure.Email;

/// <summary>
/// Hosted background service that drains <see cref="IEmailQueueReader"/>, sending each
/// <see cref="EmailMessage"/> through the SMTP resilience pipeline and storing failures
/// via <see cref="IFailedEmailStore"/> for later retry.
/// </summary>
public sealed class EmailSendingBackgroundService : QueueConsumerBackgroundService<EmailMessage>
{
    private readonly IEmailSender _sender;
    private readonly ResiliencePipelineProvider<string> _resiliencePipelineProvider;
    private readonly IFailedEmailStore _failedEmailStore;
    private readonly ILogger<EmailSendingBackgroundService> _logger;

    public EmailSendingBackgroundService(
        IEmailQueueReader queue,
        IEmailSender sender,
        ResiliencePipelineProvider<string> resiliencePipelineProvider,
        IFailedEmailStore failedEmailStore,
        ILogger<EmailSendingBackgroundService> logger
    )
        : base(queue)
    {
        _sender = sender;
        _resiliencePipelineProvider = resiliencePipelineProvider;
        _failedEmailStore = failedEmailStore;
        _logger = logger;
    }

    /// <summary>Executes delivery of <paramref name="message"/> through the configured SMTP resilience pipeline.</summary>
    protected override async Task ProcessItemAsync(EmailMessage message, CancellationToken ct)
    {
        var pipeline = _resiliencePipelineProvider.GetPipeline(ResiliencePipelineKeys.SmtpSend);

        await pipeline.ExecuteAsync(
            async token =>
            {
                await _sender.SendAsync(message, token);
            },
            ct
        );
    }

    /// <summary>Logs the final send failure and delegates to <see cref="IFailedEmailStore"/> to persist the message for retry.</summary>
    protected override async Task HandleErrorAsync(
        EmailMessage message,
        Exception ex,
        CancellationToken ct
    )
    {
        _logger.LogError(
            ex,
            "Failed to send email to {Recipient} with subject '{Subject}' after all retry attempts.",
            message.To,
            message.Subject
        );

        await _failedEmailStore.StoreFailedAsync(message, ex.Message, ct);
    }
}
