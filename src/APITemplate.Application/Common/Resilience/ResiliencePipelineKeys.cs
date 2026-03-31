namespace APITemplate.Application.Common.Resilience;

/// <summary>
/// String constants that identify the named Polly resilience pipelines registered in the application.
/// Use these keys when resolving a pipeline from <c>ResiliencePipelineProvider</c>.
/// </summary>
public static class ResiliencePipelineKeys
{
    public const string MongoProductDataDelete =
        SharedKernel.Application.Resilience.ResiliencePipelineKeys.MongoProductDataDelete;
    public const string SmtpSend = SharedKernel.Application.Resilience.ResiliencePipelineKeys.SmtpSend;
    public const string KeycloakAdmin =
        SharedKernel.Application.Resilience.ResiliencePipelineKeys.KeycloakAdmin;
    public const string KeycloakReadiness =
        SharedKernel.Application.Resilience.ResiliencePipelineKeys.KeycloakReadiness;
    public const string OutgoingWebhook =
        SharedKernel.Application.Resilience.ResiliencePipelineKeys.OutgoingWebhook;
}
