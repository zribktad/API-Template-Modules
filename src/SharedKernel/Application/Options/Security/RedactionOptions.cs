using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace SharedKernel.Application.Options.Security;

/// <summary>
/// Configuration for the HMAC-based data redaction feature used to pseudonymise sensitive fields.
/// The signing key is sourced from an environment variable whose name is specified here.
/// </summary>
public sealed class RedactionOptions
{
    [Description("Environment variable name from which the redaction HMAC key should be read.")]
    [Required]
    [MinLength(1)]
    public string HmacKeyEnvironmentVariable { get; init; } = "APITEMPLATE_REDACTION_HMAC_KEY";

    [Description(
        "Inline fallback HMAC key used for redaction when the environment variable is not set."
    )]
    public string HmacKey { get; init; } = string.Empty;

    [Description(
        "Identifier attached to redacted values so the active HMAC key version can be tracked."
    )]
    [Range(1, int.MaxValue)]
    public int KeyId { get; init; } = 1001;
}
