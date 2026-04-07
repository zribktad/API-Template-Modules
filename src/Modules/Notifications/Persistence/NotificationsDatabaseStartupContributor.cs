using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Persistence;
using SharedKernel.Infrastructure.Startup;

namespace Notifications.Persistence;

internal sealed class NotificationsDatabaseStartupContributor : IDatabaseStartupContributor
{
    public int Order => 60;

    public async Task ApplyAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken
    )
    {
        NotificationsDbContext context =
            serviceProvider.GetRequiredService<NotificationsDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }
}
