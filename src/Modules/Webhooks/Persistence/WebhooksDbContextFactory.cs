using BuildingBlocks.Infrastructure.EFCore.Persistence.DesignTime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Webhooks.Persistence;

public class WebhooksDbContextFactory : IDesignTimeDbContextFactory<WebhooksDbContext>
{
    public WebhooksDbContext CreateDbContext(string[] args)
    {
        string connectionString = DesignTimeConnectionStringResolver.Resolve();

        DbContextOptions<WebhooksDbContext> options =
            new DbContextOptionsBuilder<WebhooksDbContext>()
                .UseNpgsql(
                    connectionString,
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "webhooks")
                )
                .Options;

        return new WebhooksDbContext(options);
    }
}
