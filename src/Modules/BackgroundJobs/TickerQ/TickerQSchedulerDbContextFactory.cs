using BuildingBlocks.Infrastructure.EFCore.Persistence.DesignTime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BackgroundJobs.TickerQ;

public sealed class TickerQSchedulerDbContextFactory
    : IDesignTimeDbContextFactory<TickerQSchedulerDbContext>
{
    public TickerQSchedulerDbContext CreateDbContext(string[] args)
    {
        string connectionString = DesignTimeConnectionStringResolver.Resolve();

        DbContextOptions<TickerQSchedulerDbContext> options =
            new DbContextOptionsBuilder<TickerQSchedulerDbContext>()
                .UseNpgsql(
                    connectionString,
                    npgsql =>
                        npgsql.MigrationsHistoryTable(
                            "__EFMigrationsHistory",
                            TickerQSchedulerOptions.DefaultSchemaName
                        )
                )
                .Options;

        return new TickerQSchedulerDbContext(options);
    }
}
