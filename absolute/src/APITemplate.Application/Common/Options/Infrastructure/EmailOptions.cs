namespace APITemplate.Application.Common.Options.Infrastructure;

/// <summary>
/// Configuration for the outbound SMTP email service, including connection settings, sender identity,
/// and retry behaviour.
/// </summary>
public sealed class EmailOptions
{
    public string SmtpHost { get; set; } = "localhost";
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public int InvitationTokenExpiryHours { get; set; } = 72;
    public string BaseUrl { get; set; } = string.Empty;
    public int MaxRetryAttempts { get; set; } = 3;
    public int RetryBaseDelaySeconds { get; set; } = 2;
}
