using Notifications.Contracts;
using Polly.Registry;
using SharedKernel.Application.Resilience;
using SharedKernel.Infrastructure.Resilience;

namespace Notifications.Services;

public sealed class SmtpSendPipelineProvider(ResiliencePipelineProvider<string> provider)
    : ResiliencePipelineProviderBase(provider, ResiliencePipelineKeys.SmtpSend),
      ISmtpSendPipelineProvider;
