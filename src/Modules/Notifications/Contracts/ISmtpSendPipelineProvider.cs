using Polly;

namespace Notifications.Contracts;

/// <summary>
///     Provides the configured SMTP send resilience pipeline for Notifications email delivery flows.
/// </summary>
public interface ISmtpSendPipelineProvider
{
    ResiliencePipeline Get();
}
