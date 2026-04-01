using SharedKernel.Application.Options.BackgroundJobs;
using BackgroundJobs.Application.Services;
using BackgroundJobs.Domain;
using BackgroundJobs.Infrastructure.Persistence;
using BackgroundJobs.Infrastructure.Repositories;
using BackgroundJobs.Infrastructure.Services;
using BackgroundJobs.Infrastructure.TickerQ;
using BackgroundJobs.Infrastructure.TickerQ.Coordination;
using BackgroundJobs.Infrastructure.TickerQ.Jobs;
using BackgroundJobs.Infrastructure.TickerQ.RecurringJobRegistrations;
using BackgroundJobs.Infrastructure.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SharedKernel.Application.BackgroundJobs;
using SharedKernel.Application.Options.Infrastructure;
using SharedKernel.Domain.Entities.Contracts;
using SharedKernel.Infrastructure.Configuration;
using SharedKernel.Infrastructure.Registration;
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
        string connectionString = configuration.GetConnectionString(ConfigurationSections.DefaultConnection)!;

        services
            .AddModule<BackgroundJobsDbContext>(configuration)
            .ConfigureDbContext((_, options) => options.UseNpgsql(connectionString))
            .AddDefaultInfrastructure()
            .AddRepository<IJobExecutionRepository, JobExecutionRepository>();

        services.AddSingleton<IValidateOptions<BackgroundJobsOptions>, BackgroundJobsOptionsValidator>();
        services.AddValidatedOptions<BackgroundJobsOptions>(configuration, validateDataAnnotations: false);
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
        services.AddScoped<IExternalIntegrationSyncService, ExternalIntegrationSyncServicePreview>();

        RegisterSoftDeleteCleanupStrategies(services);
        RegisterTickerQRuntime(services, configuration, options);

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

        string? dragonflyConnectionString = configuration
            .SectionFor<DragonflyOptions>()
            .GetValue<string>(nameof(DragonflyOptions.ConnectionString));

        if (string.IsNullOrWhiteSpace(dragonflyConnectionString))
        {
            throw new InvalidOperationException(
                "Background jobs require Dragonfly:ConnectionString when BackgroundJobs:TickerQ:Enabled is true.");
        }

        string connectionString = configuration.GetConnectionString(ConfigurationSections.DefaultConnection)!;
        string schemaName = TickerQSchedulerOptions.DefaultSchemaName;

        services.AddDbContext<TickerQSchedulerDbContext>(dbOptions =>
            dbOptions.UseNpgsql(connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schemaName)));

        services.AddScoped<TickerQRecurringJobRegistrar>();
        services.AddSingleton<IDistributedJobCoordinator, DragonflyDistributedJobCoordinator>();
        services.AddScoped<IRecurringBackgroundJobRegistration, ExternalSyncRecurringJobRegistration>();
        services.AddScoped<IRecurringBackgroundJobRegistration, CleanupRecurringJobRegistration>();
        services.AddScoped<IRecurringBackgroundJobRegistration, ReindexRecurringJobRegistration>();

        services.AddTickerQ(tickerOptions =>
        {
            tickerOptions
                .AddOperationalStore(store =>
                    store
                        .UseApplicationDbContext<TickerQSchedulerDbContext>(ConfigurationType.IgnoreModelCustomizer)
                        .SetSchema(schemaName))
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
        IEnumerable<Type> softDeletableTypes = typeof(ISoftDeletable)
            .Assembly.GetTypes()
            .Where(t =>
                t is { IsClass: true, IsAbstract: false }
                && typeof(ISoftDeletable).IsAssignableFrom(t))
            .OrderBy(t => t.Name);

        foreach (Type entityType in softDeletableTypes)
        {
            Type strategyType = typeof(SoftDeleteCleanupStrategy<>).MakeGenericType(entityType);
            services.AddScoped(typeof(ISoftDeleteCleanupStrategy), strategyType);
        }
    }
}
