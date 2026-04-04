using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace BackgroundJobs.TickerQ;

public sealed class TickerQSchedulerDbContextFactory
    : IDesignTimeDbContextFactory<TickerQSchedulerDbContext>
{
    public TickerQSchedulerDbContext CreateDbContext(string[] args)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true)
            .AddJsonFile("appsettings.Development.json", true)
            .AddEnvironmentVariables()
            .Build();

        string connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is required. "
                    + "Set it in appsettings.json or via the ConnectionStrings__DefaultConnection environment variable."
            );

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
