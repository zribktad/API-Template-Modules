using APITemplate.Application.Common.Options;
using APITemplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace APITemplate.Infrastructure.BackgroundJobs.TickerQ;

/// <summary>
/// Design-time factory for <see cref="TickerQSchedulerDbContext"/>, enabling EF Core CLI
/// migration commands (<c>dotnet ef migrations add</c>) without a running host.
/// Connection string is resolved from <c>appsettings.json</c>, <c>appsettings.Development.json</c>,
/// and environment variables, falling back to a local development default.
/// </summary>
public sealed class TickerQSchedulerDbContextFactory
    : IDesignTimeDbContextFactory<TickerQSchedulerDbContext>
{
    /// <summary>Creates a configured <see cref="TickerQSchedulerDbContext"/> for tooling use.</summary>
    public TickerQSchedulerDbContext CreateDbContext(string[] args)
    {
        var configuration = DesignTimeConfigurationHelper.BuildConfiguration();
        var connectionString = DesignTimeConfigurationHelper.GetConnectionString(configuration);

        var options = new DbContextOptionsBuilder<TickerQSchedulerDbContext>()
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
