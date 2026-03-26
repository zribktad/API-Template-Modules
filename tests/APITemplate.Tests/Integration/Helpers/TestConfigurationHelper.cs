using System.Security.Cryptography;
using System.Text;

namespace APITemplate.Tests.Integration.Helpers;

internal static class TestConfigurationHelper
{
    internal const string TestWebhookSecret = "test-webhook-secret-at-least-16-chars";

    internal static Dictionary<string, string?> GetBaseConfiguration(
        string hmacKeySeed = "APITemplate.Tests.RedactionKey"
    )
    {
        var testRedactionHmacKey = Convert.ToBase64String(
            SHA256.HashData(Encoding.UTF8.GetBytes(hmacKeySeed))
        );

        return new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] =
                "Host=localhost;Database=apitemplate_tests;Username=postgres;Password=postgres",
            ["BackgroundJobs:TickerQ:Enabled"] = "false",
            ["Keycloak:realm"] = "api-template",
            ["Keycloak:auth-server-url"] = "http://localhost:8180/",
            ["Keycloak:resource"] = "api-template",
            ["Keycloak:credentials:secret"] = "test-secret",
            ["Keycloak:SkipReadinessCheck"] = "true",
            ["SystemIdentity:DefaultActorId"] = "00000000-0000-0000-0000-000000000000",
            ["Bootstrap:Tenant:Code"] = "default",
            ["Bootstrap:Tenant:Name"] = "Default Tenant",
            ["Cors:AllowedOrigins:0"] = "http://localhost:3000",
            ["Persistence:Transactions:IsolationLevel"] = "ReadCommitted",
            ["Persistence:Transactions:TimeoutSeconds"] = "30",
            ["Persistence:Transactions:RetryEnabled"] = "true",
            ["Persistence:Transactions:RetryCount"] = "3",
            ["Persistence:Transactions:RetryDelaySeconds"] = "5",
            ["Redaction:HmacKeyEnvironmentVariable"] = "APITEMPLATE_REDACTION_HMAC_KEY",
            ["Redaction:HmacKey"] = testRedactionHmacKey,
            ["Redaction:KeyId"] = "1001",
            ["Observability:ServiceName"] = "APITemplate.Tests",
            ["Observability:Exporters:Aspire:Enabled"] = "false",
            ["Observability:Exporters:Otlp:Enabled"] = "false",
            ["Observability:Exporters:Console:Enabled"] = "false",
            ["Dragonfly:ConnectionString"] = "",
            ["Webhook:Secret"] = TestWebhookSecret,
        };
    }
}
