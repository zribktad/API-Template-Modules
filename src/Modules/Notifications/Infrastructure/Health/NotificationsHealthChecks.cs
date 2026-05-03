using BuildingBlocks.Web.Health;
using MailKit.Net.Smtp;
using Microsoft.Extensions.DependencyInjection;

namespace Notifications.Infrastructure.Health;

public sealed class NotificationsHealthChecks : IHealthCheckModule
{
    public void RegisterHealthChecks(IHealthChecksBuilder builder)
    {
        builder.Services.AddTransient<Func<ISmtpClient>>(_ => () => new SmtpClient());
        builder.AddCheck<SmtpHealthCheck>(
            HealthCheckNames.Smtp,
            tags: [HealthCheckTags.Ready, HealthCheckTags.External]
        );
    }
}
