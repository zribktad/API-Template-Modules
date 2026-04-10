using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Notifications.Contracts;

namespace Notifications.Infrastructure.Health;

/// <summary>
///     Probes the configured SMTP server by connecting and disconnecting with a 5-second timeout.
/// </summary>
public sealed class SmtpHealthCheck : IHealthCheck
{
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(5);
    private readonly EmailOptions _emailOptions;
    private readonly Func<ISmtpClient> _smtpClientFactory;

    public SmtpHealthCheck(Func<ISmtpClient> smtpClientFactory, IOptions<EmailOptions> options)
    {
        _smtpClientFactory = smtpClientFactory;
        _emailOptions = options.Value;
    }

    /// <summary>
    ///     Creates a short-lived SMTP connection to verify the server is reachable.
    ///     Returns <see cref="HealthCheckResult.Healthy" /> on success,
    ///     or <see cref="HealthCheckResult.Unhealthy" /> on failure.
    /// </summary>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken
            );
            cts.CancelAfter(CheckTimeout);

            SecureSocketOptions socketOptions = _emailOptions.UseSsl
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTlsWhenAvailable;

            using ISmtpClient smtpClient = _smtpClientFactory();
            await smtpClient.ConnectAsync(
                _emailOptions.SmtpHost,
                _emailOptions.SmtpPort,
                socketOptions,
                cts.Token
            );
            await smtpClient.DisconnectAsync(true, cts.Token);

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SMTP server is not reachable", ex);
        }
    }
}
