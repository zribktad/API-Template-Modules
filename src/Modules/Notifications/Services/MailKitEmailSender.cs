using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Notifications.Contracts;
using Notifications.Errors;
using Notifications.Logging;

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
    public async Task<ErrorOr<Success>> SendAsync(
        EmailMessage message,
        CancellationToken ct = default
    )
    {
        MimeMessage mimeMessage = new();
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
                await client.ConnectAsync(
                    _options.SmtpHost,
                    _options.SmtpPort,
                    _options.UseSsl,
                    ct
                );
            }

            if (!string.IsNullOrEmpty(_options.Username) && !client.IsAuthenticated)
            {
                if (string.IsNullOrEmpty(_options.Password))
                    return Error.Failure(ErrorCatalog.Smtp.SendFailed, "SMTP password is missing.");

                await client.AuthenticateAsync(_options.Username, _options.Password, ct);
            }

            await client.SendAsync(mimeMessage, ct);
        }
        catch (OperationCanceledException)
        {
            await ResetClientAsync();
            throw;
        }
        catch (Exception ex)
        {
            await ResetClientAsync();
            return Error.Failure(ErrorCatalog.Smtp.SendFailed, ex.Message);
        }
        finally
        {
            _lock.Release();
        }

        _logger.EmailSent(message.To, message.Subject);
        return Result.Success;
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SMTP client cleanup failed during reset — ignored.");
        }
        finally
        {
            _client.Dispose();
            _client = null;
        }
    }
}
