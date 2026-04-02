using System.ComponentModel.DataAnnotations;

namespace APITemplate.Application.Common.Options.Security;

/// <summary>
/// Configuration for the HMAC-based data redaction feature used to pseudonymise sensitive fields.
/// The signing key is sourced from an environment variable whose name is specified here.
/// </summary>
public sealed class RedactionOptions
{
    [Required]
    public string HmacKeyEnvironmentVariable { get; init; } = "APITEMPLATE_REDACTION_HMAC_KEY";

    public string HmacKey { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int KeyId { get; init; } = 1001;
}
