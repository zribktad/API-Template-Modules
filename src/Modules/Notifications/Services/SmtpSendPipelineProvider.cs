using BuildingBlocks.Application.Resilience;
using BuildingBlocks.Web.Resilience;
using Notifications.Contracts;
using Polly.Registry;

namespace Notifications.Services;

public sealed class SmtpSendPipelineProvider(ResiliencePipelineProvider<string> provider)
    : ResiliencePipelineProviderBase(provider, ResiliencePipelineKeys.SmtpSend),
        ISmtpSendPipelineProvider;
