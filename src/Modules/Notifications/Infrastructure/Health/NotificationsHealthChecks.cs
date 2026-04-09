using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Infrastructure.Health;

namespace Notifications.Infrastructure.Health;

public sealed class NotificationsHealthChecks : IHealthCheckModule
{
    private readonly IConfiguration _configuration;

    public NotificationsHealthChecks(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void RegisterHealthChecks(IHealthChecksBuilder builder)
    {
        builder.Services.AddTransient<Func<ISmtpClient>>(_ => () => new SmtpClient());
        builder.AddCheck<SmtpHealthCheck>(
            HealthCheckNames.Smtp,
            tags: [HealthCheckTags.Ready, HealthCheckTags.External]
        );
    }
}
