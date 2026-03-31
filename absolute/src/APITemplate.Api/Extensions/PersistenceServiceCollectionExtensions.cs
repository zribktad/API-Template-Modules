using APITemplate.Api.Extensions.Resilience;
using APITemplate.Application.Common.Options;
using APITemplate.Application.Common.Resilience;
using APITemplate.Application.Common.Security;
using APITemplate.Application.Common.Startup;
using APITemplate.Application.Features.Product.Repositories;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Health;
using APITemplate.Infrastructure.Persistence;
using APITemplate.Infrastructure.Persistence.Auditing;
using APITemplate.Infrastructure.Persistence.EntityNormalization;
using APITemplate.Infrastructure.Persistence.SoftDelete;
using APITemplate.Infrastructure.Persistence.Startup;
using APITemplate.Infrastructure.Repositories;
using APITemplate.Infrastructure.Security;
using APITemplate.Infrastructure.StoredProcedures;
using Kot.MongoDB.Migrations;
using Kot.MongoDB.Migrations.DI;
using Microsoft.EntityFrameworkCore;
using Polly;

namespace APITemplate.Api.Extensions;

/// <summary>
/// Presentation-layer extension class that registers EF Core (PostgreSQL), MongoDB, and all
/// related repository, auditing, soft-delete, and startup seeding services.
/// </summary>
public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Configures <see cref="AppDbContext"/>, registers all repository and infrastructure
    /// services, and adds a PostgreSQL health check.
    /// </summary>
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var connectionString = configuration.GetConnectionString(
            ConfigurationSections.DefaultConnection
        )!;

        services.Configure<TransactionDefaultsOptions>(
            configuration.SectionFor<TransactionDefaultsOptions>()
        );

        services.AddDbContext<AppDbContext>(options =>
            ConfigurePostgresDbContext(options, connectionString)
        );

        // Repositories (data access)
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductDataLinkRepository, ProductDataLinkRepository>();
        services.AddScoped<IProductReviewRepository, ProductReviewRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ITenantInvitationRepository, TenantInvitationRepository>();
        services.AddScoped<IStoredFileRepository, StoredFileRepository>();
        services.AddScoped<IJobExecutionRepository, JobExecutionRepository>();

        // Infrastructure / persistence helpers
        services.AddScoped<IStoredProcedureExecutor, StoredProcedureExecutor>();
        services.AddScoped<IDbTransactionProvider, EfCoreTransactionProvider>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IStartupTaskCoordinator, PostgresAdvisoryLockStartupTaskCoordinator>();

        // Auditing / normalization / soft delete behavior
        services.AddSingleton<IEntityNormalizationService, AppUserEntityNormalizationService>();
        services.AddSingleton<IAuditableEntityStateManager, AuditableEntityStateManager>();
        services.AddSingleton<ISoftDeleteProcessor, SoftDeleteProcessor>();
        services.AddScoped<ISoftDeleteCascadeRule, ProductSoftDeleteCascadeRule>();
        services.AddScoped<ISoftDeleteCascadeRule, TenantSoftDeleteCascadeRule>();

        // Application services / initialization
        services.AddScoped<IUserProvisioningService, UserProvisioningService>();
        services.AddScoped<AuthBootstrapSeeder>();

        // System services
        services.AddSingleton(TimeProvider.System);

        services
            .AddHealthChecks()
            .AddNpgSql(connectionString, name: HealthCheckNames.PostgreSql, tags: ["database"]);

        return services;
    }

    /// <summary>
    /// Applies the Npgsql provider to the given <see cref="DbContextOptionsBuilder"/>; exposed
    /// internally so integration tests can reuse the same configuration logic.
    /// </summary>
    internal static void ConfigurePostgresDbContext(
        DbContextOptionsBuilder options,
        string connectionString
    )
    {
        options.UseNpgsql(connectionString);
    }

    /// <summary>
    /// Registers the MongoDB context, product-data repository, a Polly retry pipeline for
    /// delete operations, the Kot.MongoDB.Migrations migrator, and a MongoDB health check.
    /// </summary>
    public static IServiceCollection AddMongoDB(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var mongoSettings = configuration
            .GetSection(ConfigurationSections.MongoDB)
            .Get<MongoDbSettings>()!;

        services.Configure<MongoDbSettings>(
            configuration.GetSection(ConfigurationSections.MongoDB)
        );
        services.AddSingleton<MongoDbContext>();
        services.AddScoped<IProductDataRepository, ProductDataRepository>();

        services.AddResiliencePipeline(
            ResiliencePipelineKeys.MongoProductDataDelete,
            builder =>
            {
                builder.AddRetry(
                    new()
                    {
                        MaxRetryAttempts = ResilienceDefaults.MaxRetryAttempts,
                        BackoffType = DelayBackoffType.Exponential,
                        Delay = ResilienceDefaults.ShortDelay,
                        UseJitter = true,
                    }
                );
            }
        );

        services.AddMongoMigrations(
            mongoSettings.ConnectionString,
            new MigrationOptions(mongoSettings.DatabaseName),
            config =>
                config.LoadMigrationsFromAssembly(
                    typeof(PersistenceServiceCollectionExtensions).Assembly
                )
        );

        services
            .AddHealthChecks()
            .AddCheck<MongoDbHealthCheck>(HealthCheckNames.MongoDb, tags: ["database"]);

        return services;
    }
}
