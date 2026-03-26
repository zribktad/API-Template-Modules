using APITemplate.Application.Common.Options.Security;
using APITemplate.Infrastructure.Logging;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Logging;

public class RedactionConfigurationTests
{
    [Fact]
    public void ResolveHmacKey_WhenEnvironmentVariableExists_ReturnsEnvironmentValue()
    {
        var options = new RedactionOptions
        {
            HmacKeyEnvironmentVariable = "APITEMPLATE_REDACTION_HMAC_KEY",
            HmacKey = "dev-key",
        };

        var key = RedactionConfiguration.ResolveHmacKey(
            options,
            name => name == "APITEMPLATE_REDACTION_HMAC_KEY" ? "prod-key" : null
        );

        key.ShouldBe("prod-key");
    }

    [Fact]
    public void ResolveHmacKey_WithoutEnvironmentVariable_ReturnsConfiguredKey()
    {
        var options = new RedactionOptions
        {
            HmacKeyEnvironmentVariable = "APITEMPLATE_REDACTION_HMAC_KEY",
            HmacKey = "dev-key",
        };

        var key = RedactionConfiguration.ResolveHmacKey(options, _ => null);

        key.ShouldBe("dev-key");
    }

    [Fact]
    public void ResolveHmacKey_WithoutEnvironmentVariableAndConfiguredKey_Throws()
    {
        var options = new RedactionOptions
        {
            HmacKeyEnvironmentVariable = "APITEMPLATE_REDACTION_HMAC_KEY",
            HmacKey = string.Empty,
        };

        var ex = Should.Throw<InvalidOperationException>(() =>
            RedactionConfiguration.ResolveHmacKey(options, _ => null)
        );

        ex.Message.ShouldContain("APITEMPLATE_REDACTION_HMAC_KEY");
    }
}
