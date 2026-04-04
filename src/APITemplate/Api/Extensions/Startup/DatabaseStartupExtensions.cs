using BackgroundJobs.Persistence;
using BackgroundJobs.TickerQ;
using FileStorage.Persistence;
using Identity.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
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
        await EnsureSchemaIfRegisteredAsync<TickerQSchedulerDbContext>(sp);

        AuthBootstrapSeeder seeder = sp.GetRequiredService<AuthBootstrapSeeder>();
        await seeder.SeedAsync();
    }

    private static async Task EnsureSchemaAsync<TContext>(IServiceProvider sp)
        where TContext : DbContext
    {
        TContext context = sp.GetRequiredService<TContext>();
        await EnsureSchemaCoreAsync(context);
    }

    /// <summary>
    ///     TickerQ (and its <see cref="TickerQSchedulerDbContext" />) is only registered when
    ///     BackgroundJobs:TickerQ:Enabled is true and Dragonfly is configured.
    /// </summary>
    private static async Task EnsureSchemaIfRegisteredAsync<TContext>(IServiceProvider sp)
        where TContext : DbContext
    {
        TContext? context = sp.GetService<TContext>();
        if (context is null)
            return;

        await EnsureSchemaCoreAsync(context);
    }

    private static async Task EnsureSchemaCoreAsync(DbContext context)
    {
        await context.Database.EnsureCreatedAsync();

        IRelationalDatabaseCreator creator = context.GetService<IRelationalDatabaseCreator>();

        try
        {
            await creator.CreateTablesAsync();
        }
        catch (PostgresException ex) when (ex.SqlState == "42P07")
        {
            // 42P07 = relation already exists — safe to ignore
        }
    }
}
