using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using BuildingBlocks.Application.Options;

namespace Webhooks.Contracts;

/// <summary>
///     Configuration for incoming webhook verification, including the shared HMAC secret
///     and the tolerance window used to reject replayed requests.
/// </summary>
public sealed class WebhookOptions : IModuleOptions
{
    public static string SectionName => "Webhook";

    [Description("Shared HMAC secret used to verify incoming webhook signatures.")]
    [Required]
    [MinLength(16, ErrorMessage = "Incoming webhook secret must be at least 16 characters.")]
    public string IncomingSecret { get; set; } = string.Empty;

    [Description("Shared HMAC secret used to sign outgoing webhook signatures.")]
    [Required]
    [MinLength(16, ErrorMessage = "Outgoing webhook secret must be at least 16 characters.")]
    public string OutgoingSecret { get; set; } = string.Empty;

    [Description("Maximum tolerated clock skew, in seconds, when validating webhook timestamps.")]
    [Range(0, int.MaxValue)]
    public int TimestampToleranceSeconds { get; set; } = 300; // 5 minutes

    [Description(
        "Whether to allow outbound webhook requests to local and private networks (intended for development)."
    )]
    public bool AllowLocalRequests { get; set; } = false;
}
