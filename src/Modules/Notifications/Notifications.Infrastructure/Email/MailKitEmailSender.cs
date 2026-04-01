using Notifications.Application.Common.Email;

using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Notifications.Infrastructure.Email;

/// <summary>
/// Infrastructure implementation of <see cref="IEmailSender"/> that delivers email over SMTP
/// using MailKit, with optional authentication and TLS controlled by <see cref="EmailOptions"/>.
/// </summary>
public sealed class MailKitEmailSender : IEmailSender, IAsyncDisposable
{
    private readonly EmailOptions _options;
    private readonly ILogger<MailKitEmailSender> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private SmtpClient? _client;

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

        await _lock.WaitAsync(ct);
        try
        {
            SmtpClient client = _client ??= new SmtpClient();

            if (!client.IsConnected)
            {
                await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, _options.UseSsl, ct);
            }

            if (!string.IsNullOrEmpty(_options.Username) && !client.IsAuthenticated)
            {
                await client.AuthenticateAsync(_options.Username, _options.Password, ct);
            }

            await client.SendAsync(mimeMessage, ct);
        }
        catch
        {
            await ResetClientAsync();
            throw;
        }
        finally
        {
            _lock.Release();
        }

        _logger.LogInformation(
            "Email sent to {Recipient} with subject '{Subject}'.",
            message.To,
            message.Subject
        );
    }

    private async Task ResetClientAsync()
    {
        if (_client is null)
        {
            return;
        }

        try
        {
            if (_client.IsConnected)
            {
                await _client.DisconnectAsync(quit: true, CancellationToken.None);
            }
        }
        catch
        {
            // Ignore cleanup failures; the caller is already handling the original send failure.
        }
        finally
        {
            _client.Dispose();
            _client = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await ResetClientAsync();
        }
        finally
        {
            _lock.Release();
            _lock.Dispose();
        }
    }
}
