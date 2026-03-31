using APITemplate.Application.Common.Email;
using APITemplate.Application.Common.Options;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace APITemplate.Infrastructure.Email;

/// <summary>
/// Infrastructure implementation of <see cref="IEmailSender"/> that delivers email over SMTP
/// using MailKit, with optional authentication and TLS controlled by <see cref="EmailOptions"/>.
/// </summary>
public sealed class MailKitEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<MailKitEmailSender> _logger;

    public MailKitEmailSender(IOptions<EmailOptions> options, ILogger<MailKitEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Builds a MIME message, connects and optionally authenticates against the configured SMTP server,
    /// sends the message, and disconnects cleanly before returning.
    /// </summary>
    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(_options.SenderName, _options.SenderEmail));
        mimeMessage.To.Add(MailboxAddress.Parse(message.To));
        mimeMessage.Subject = message.Subject;
        mimeMessage.Body = new TextPart("html") { Text = message.HtmlBody };

        using var client = new SmtpClient();
        await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, _options.UseSsl, ct);

        if (!string.IsNullOrEmpty(_options.Username))
            await client.AuthenticateAsync(_options.Username, _options.Password, ct);

        await client.SendAsync(mimeMessage, ct);
        await client.DisconnectAsync(quit: true, ct);

        _logger.LogInformation(
            "Email sent to {Recipient} with subject '{Subject}'.",
            message.To,
            message.Subject
        );
    }
}
