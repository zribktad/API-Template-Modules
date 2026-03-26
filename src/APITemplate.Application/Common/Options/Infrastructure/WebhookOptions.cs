using System.ComponentModel.DataAnnotations;

namespace APITemplate.Application.Common.Options.Infrastructure;

/// <summary>
/// Configuration for incoming webhook verification, including the shared HMAC secret
/// and the tolerance window used to reject replayed requests.
/// </summary>
public sealed class WebhookOptions
{
    [Required]
    [MinLength(16, ErrorMessage = "Webhook secret must be at least 16 characters.")]
    public string Secret { get; set; } = string.Empty;

    public int TimestampToleranceSeconds { get; set; } = 300; // 5 minutes
}
