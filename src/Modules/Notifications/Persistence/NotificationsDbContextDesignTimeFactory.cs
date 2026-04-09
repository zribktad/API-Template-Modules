using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SharedKernel.Infrastructure.Persistence.DesignTime;

namespace Notifications.Persistence;

/// <summary>
///     Design-time factory for <c>dotnet ef migrations</c> against <see cref="NotificationsDbContext" />.
/// </summary>
public sealed class NotificationsDbContextDesignTimeFactory
    : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args)
    {
        string connectionString = DesignTimeConnectionStringResolver.Resolve();

        DbContextOptions<NotificationsDbContext> options =
            new DbContextOptionsBuilder<NotificationsDbContext>()
                .UseNpgsql(connectionString)
                .Options;

        return new NotificationsDbContext(
            options,
            DesignTimeServices.TenantProvider,
            DesignTimeServices.ActorProvider,
            TimeProvider.System,
            DesignTimeServices.AuditableEntityStateManager
        );
    }
}
