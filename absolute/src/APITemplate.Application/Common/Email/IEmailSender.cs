namespace APITemplate.Application.Common.Email;

/// <summary>
/// Application-layer abstraction for sending emails, decoupling the Application layer from
/// any specific mail provider (SMTP, SendGrid, AWS SES, etc.).
/// </summary>
public interface IEmailSender
{
    /// <summary>Transmits <paramref name="message"/> to its recipient via the configured mail provider.</summary>
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
