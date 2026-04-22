using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Notifications.Contracts;
using Notifications.Logging;
using SharedKernel.Application.Errors;
using NTF = Notifications.Errors.ErrorCatalog;

namespace Notifications.Services;

/// <summary>
///     Infrastructure implementation of <see cref="IEmailSender" /> that delivers email over SMTP
///     using MailKit, with optional authentication and TLS controlled by <see cref="EmailOptions" />.
/// </summary>
public sealed class MailKitEmailSender : IEmailSender, IAsyncDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<MailKitEmailSender> _logger;
    private readonly EmailOptions _options;
    private SmtpClient? _client;

    public MailKitEmailSender(IOptions<EmailOptions> options, ILogger<MailKitEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
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

    /// <summary>
    ///     Builds a MIME message, connects and optionally authenticates against the configured SMTP server,
    ///     sends the message, and disconnects cleanly before returning.
    /// </summary>
    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        MimeMessage mimeMessage = new();
        mimeMessage.From.Add(new MailboxAddress(_options.SenderName, _options.SenderEmail));
        mimeMessage.To.Add(MailboxAddress.Parse(message.To));
        mimeMessage.Subject = message.Subject;
        mimeMessage.Body = new TextPart("html") { Text = message.HtmlBody };

        if (!string.IsNullOrEmpty(_options.Username) && string.IsNullOrEmpty(_options.Password))
            throw new AppException("SMTP password is missing.", NTF.Smtp.SendFailed);

        await _lock.WaitAsync(ct);
        try
        {
            SmtpClient client = _client ??= new SmtpClient();

            if (!client.IsConnected)
            {
                await client.ConnectAsync(
                    _options.SmtpHost,
                    _options.SmtpPort,
                    _options.UseSsl,
                    ct
                );
            }

            if (!string.IsNullOrEmpty(_options.Username) && !client.IsAuthenticated)
            {
                await client.AuthenticateAsync(_options.Username, _options.Password!, ct);
            }

            await client.SendAsync(mimeMessage, ct);
        }
        catch (OperationCanceledException)
        {
            await ResetClientAsync();
            throw;
        }
        catch (AuthenticationException ex)
        {
            _logger.SmtpSendFailed(ex, message.To);
            await ResetClientAsync();
            throw new AppException(
                "SMTP authentication failed.",
                NTF.Smtp.SendFailed,
                innerException: ex
            );
        }
        catch (Exception ex)
        {
            _logger.SmtpSendFailed(ex, message.To);
            await ResetClientAsync();
            throw new AppException(
                $"SMTP send failed: {ex.GetType().Name}",
                NTF.Smtp.SendFailed,
                innerException: ex
            );
        }
        finally
        {
            _lock.Release();
        }

        _logger.EmailSent(message.To, message.Subject);
    }

    private async Task ResetClientAsync()
    {
        if (_client is null)
            return;

        try
        {
            if (_client.IsConnected)
                await _client.DisconnectAsync(true, CancellationToken.None);
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
}
