using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using BuildingBlocks.Web.Logging;

namespace Notifications.Contracts;

/// <summary>
///     Configuration for the outbound SMTP email service, including connection settings, sender identity,
///     and retry behaviour.
/// </summary>
public sealed class EmailOptions
{
    [Description("SMTP server hostname used for outbound email delivery.")]
    [Required]
    [MinLength(1)]
    public string SmtpHost { get; set; } = "localhost";

    [Description("SMTP server port used for outbound email delivery.")]
    [Range(1, 65535)]
    public int SmtpPort { get; set; } = 587;

    [Description("Enables SSL or TLS when connecting to the SMTP server.")]
    public bool UseSsl { get; set; } = true;

    [Description("Sender email address used in outbound notification messages.")]
    [Required]
    [EmailAddress]
    public string SenderEmail { get; set; } = string.Empty;

    [Description("Sender display name used in outbound notification messages.")]
    [Required]
    [MinLength(1)]
    public string SenderName { get; set; } = string.Empty;

    [Description("Optional SMTP username used for authenticated delivery.")]
    [SensitiveData]
    public string? Username { get; set; }

    [Description("Optional SMTP password used for authenticated delivery.")]
    [SensitiveData]
    public string? Password { get; set; }

    [Description("Public application base URL used when generating links in email templates.")]
    [Required]
    [MinLength(1)]
    public string BaseUrl { get; set; } = string.Empty;

    [Description("Maximum number of retry attempts for failed email delivery.")]
    [Range(1, int.MaxValue)]
    public int MaxRetryAttempts { get; set; } = 3;

    [Description("Base delay, in seconds, used by exponential backoff between retry attempts.")]
    [Range(1, int.MaxValue)]
    public int RetryBaseDelaySeconds { get; set; } = 2;
}
