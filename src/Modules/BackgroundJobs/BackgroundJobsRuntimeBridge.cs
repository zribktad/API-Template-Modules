using System.Reflection;
using BackgroundJobs.Contracts;
using BackgroundJobs.Persistence;
using BackgroundJobs.Repositories;
using BackgroundJobs.Services;
using BackgroundJobs.TickerQ;
using BackgroundJobs.TickerQ.Jobs;
using BackgroundJobs.TickerQ.RecurringJobRegistrations;
using BackgroundJobs.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SharedKernel.Application.Options.Infrastructure;
using SharedKernel.Infrastructure.Configuration;
using SharedKernel.Infrastructure.Registration;
using SharedKernel.Infrastructure.Startup;
using TickerQ.DependencyInjection;
using TickerQ.EntityFrameworkCore.Customizer;
using TickerQ.EntityFrameworkCore.DependencyInjection;

namespace BackgroundJobs;

public static class BackgroundJobsRuntimeBridge
{
    public static IServiceCollection AddBackgroundJobsRuntimeBridge(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        string connectionString = configuration.GetConnectionString(
            ConfigurationSections.DefaultConnection
        )!;

        services
            .AddModule<BackgroundJobsDbContext>(configuration)
            .ConfigureDbContext(options => options.UseNpgsql(connectionString))
            .AddDefaultInfrastructure()
            .ForwardUnitOfWork<BackgroundJobsDbMarker>()
            .AddRepository<IJobExecutionRepository, JobExecutionRepository>();

        services.AddSingleton<
            IValidateOptions<BackgroundJobsOptions>,
            BackgroundJobsOptionsValidator
        >();
        services.AddValidatedOptions<BackgroundJobsOptions>(configuration, false);
        services.AddValidatedOptions<EmailRetryJobOptions>(
            configuration
                .SectionFor<BackgroundJobsOptions>()
                .GetSection(nameof(BackgroundJobsOptions.EmailRetry))
        );
        BackgroundJobsOptions options =
            configuration.SectionFor<BackgroundJobsOptions>().Get<BackgroundJobsOptions>()
            ?? new BackgroundJobsOptions();

        services.AddQueueWithConsumer<
            ChannelJobQueue,
            IJobQueue,
            IJobQueueReader,
            JobProcessingBackgroundService
        >();

        services.AddScoped<ICleanupService, CleanupService>();
        services.AddScoped<IReindexService, ReindexService>();
        services.AddScoped<IEmailRetryJobService, EmailRetryJobService>();
        services.AddScoped<IOrphanBlobJobService, OrphanBlobJobService>();
        services.AddScoped<
            IExternalIntegrationSyncService,
            ExternalIntegrationSyncServicePreview
        >();

        RegisterSoftDeleteCleanupStrategies(services);
        RegisterTickerQRuntime(services, configuration, options);

        services.AddSingleton<
            IDatabaseStartupContributor,
            BackgroundJobsDatabaseStartupContributor
        >();
        services.AddSingleton<
            IDatabaseStartupContributor,
            TickerQSchedulerDatabaseStartupContributor
        >();

        return services;
    }

    private static void RegisterTickerQRuntime(
        IServiceCollection services,
        IConfiguration configuration,
        BackgroundJobsOptions options
    )
    {
        if (!options.TickerQ.Enabled)
            return;

        string connectionString = configuration.GetConnectionString(
            ConfigurationSections.DefaultConnection
        )!;
        string schemaName = TickerQSchedulerOptions.DefaultSchemaName;

        services.AddDbContext<TickerQSchedulerDbContext>(dbOptions =>
            dbOptions.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schemaName)
            )
        );

        services.AddScoped<TickerQRecurringJobRegistrar>();
        if (configuration.IsRedisConfigured())
        {
            services.AddSingleton<IDistributedJobCoordinator, RedisDistributedJobCoordinator>();
        }
        else
        {
            services.AddSingleton<IDistributedJobCoordinator, LocalSingleProcessJobCoordinator>();
        }
        services.AddScoped<
            IRecurringBackgroundJobRegistration,
            ExternalSyncRecurringJobRegistration
        >();
        services.AddScoped<IRecurringBackgroundJobRegistration, CleanupRecurringJobRegistration>();
        services.AddScoped<IRecurringBackgroundJobRegistration, ReindexRecurringJobRegistration>();
        services.AddScoped<
            IRecurringBackgroundJobRegistration,
            EmailRetryRecurringJobRegistration
        >();
        services.AddScoped<
            IRecurringBackgroundJobRegistration,
            OrphanBlobRecurringJobRegistration
        >();

        services.AddTickerQ(tickerOptions =>
        {
            tickerOptions
                .AddOperationalStore(store =>
                    store
                        .UseApplicationDbContext<TickerQSchedulerDbContext>(
                            ConfigurationType.IgnoreModelCustomizer
                        )
                        .SetSchema(schemaName)
                )
                .ConfigureScheduler(scheduler =>
                {
                    scheduler.NodeIdentifier =
                        $"{options.TickerQ.InstanceNamePrefix}-{Environment.MachineName}-{Environment.ProcessId}";
                    scheduler.MaxConcurrency = 1;
                })
                .AddTickerQDiscovery([typeof(CleanupRecurringJob).Assembly]);
        });
    }

    private static void RegisterSoftDeleteCleanupStrategies(IServiceCollection services)
    {
        IEnumerable<Type> softDeletableTypes = AppDomain
            .CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic)
            .SelectMany(a =>
            {
                try
                {
                    return a.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    return ex.Types.OfType<Type>();
                }
            })
            .Where(t =>
                t is { IsClass: true, IsAbstract: false }
                && typeof(ISoftDeletable).IsAssignableFrom(t)
            )
            .OrderBy(t => t.Name);

        List<Type> dbContextTypes = services
            .Where(sd =>
                sd.ServiceType != typeof(DbContext)
                && typeof(DbContext).IsAssignableFrom(sd.ServiceType)
                && !sd.ServiceType.IsAbstract
            )
            .Select(sd => sd.ServiceType)
            .Distinct()
            .ToList();

        foreach (Type entityType in softDeletableTypes)
        {
            Type strategyType = typeof(SoftDeleteCleanupStrategy<>).MakeGenericType(entityType);
            Type capturedEntityType = entityType;

            services.AddScoped(
                typeof(ISoftDeleteCleanupStrategy),
                sp =>
                {
                    foreach (Type ctxType in dbContextTypes)
                    {
                        DbContext ctx = (DbContext)sp.GetRequiredService(ctxType);
                        if (ctx.Model.FindEntityType(capturedEntityType) is not null)
                            return Activator.CreateInstance(strategyType, ctx)!;
                    }

                    throw new InvalidOperationException(
                        $"No DbContext found for soft-deletable entity {capturedEntityType.Name}."
                    );
                }
            );
        }
    }
}
