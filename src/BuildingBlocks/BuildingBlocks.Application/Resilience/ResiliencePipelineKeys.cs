namespace BuildingBlocks.Application.Resilience;

/// <summary>
///     String constants that identify the named Polly resilience pipelines registered in the application.
///     Use these keys when resolving a pipeline from <c>ResiliencePipelineProvider</c>.
/// </summary>
public static class ResiliencePipelineKeys
{
    public const string MongoProductDataDelete = "mongo-productdata-delete";
    public const string FileStorageDelete = "file-storage-delete";
    public const string SmtpSend = "smtp-send";
    public const string KeycloakAdmin = "keycloak-admin";
    public const string KeycloakToken = "keycloak-token";
    public const string KeycloakReadiness = "keycloak-readiness";
    public const string OutgoingWebhook = "outgoing-webhook";
}

