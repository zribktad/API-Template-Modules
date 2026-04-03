using BackgroundJobs.Persistence;
using BackgroundJobs.TickerQ;
using FileStorage.Persistence;
using Identity.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using ProductCatalog.Persistence;
using Reviews.Persistence;

namespace APITemplate.Api.Extensions.Startup;

public static class DatabaseStartupExtensions
{
    public static async Task UseDatabaseAsync(this WebApplication app)
    {
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        IServiceProvider sp = scope.ServiceProvider;

        await EnsureSchemaAsync<IdentityDbContext>(sp);
        await EnsureSchemaAsync<ProductCatalogDbContext>(sp);
        await EnsureSchemaAsync<ReviewsDbContext>(sp);
        await EnsureSchemaAsync<FileStorageDbContext>(sp);
        await EnsureSchemaAsync<BackgroundJobsDbContext>(sp);
        await EnsureSchemaAsync<TickerQSchedulerDbContext>(sp);

        AuthBootstrapSeeder seeder = sp.GetRequiredService<AuthBootstrapSeeder>();
        await seeder.SeedAsync();
    }

    private static async Task EnsureSchemaAsync<TContext>(IServiceProvider sp)
        where TContext : DbContext
    {
        TContext context = sp.GetRequiredService<TContext>();

        await context.Database.EnsureCreatedAsync();

        IRelationalDatabaseCreator creator = context.GetService<IRelationalDatabaseCreator>();

        try
        {
            await creator.CreateTablesAsync();
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P07")
        {
            // 42P07 = relation already exists — safe to ignore
        }
    }
}
