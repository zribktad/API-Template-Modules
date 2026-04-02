using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace SharedKernel.Application.Options.Infrastructure;

/// <summary>
/// Configuration for incoming webhook verification, including the shared HMAC secret
/// and the tolerance window used to reject replayed requests.
/// </summary>
public sealed class WebhookOptions
{
    [Description("Shared HMAC secret used to verify incoming webhook signatures.")]
    [Required]
    [MinLength(16, ErrorMessage = "Webhook secret must be at least 16 characters.")]
    public string Secret { get; set; } = string.Empty;

    [Description("Maximum tolerated clock skew, in seconds, when validating webhook timestamps.")]
    [Range(0, int.MaxValue)]
    public int TimestampToleranceSeconds { get; set; } = 300; // 5 minutes
}
