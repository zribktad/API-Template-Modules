using APITemplate.Application.Common.BackgroundJobs;
using APITemplate.Application.Common.Options;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.BackgroundJobs.Services;
using APITemplate.Infrastructure.BackgroundJobs.TickerQ;
using APITemplate.Infrastructure.BackgroundJobs.TickerQ.Coordination;
using APITemplate.Infrastructure.BackgroundJobs.TickerQ.Jobs;
using APITemplate.Infrastructure.BackgroundJobs.TickerQ.RecurringJobRegistrations;
using APITemplate.Infrastructure.BackgroundJobs.Validation;
using APITemplate.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using TickerQ.DependencyInjection;
using TickerQ.EntityFrameworkCore.Customizer;
using TickerQ.EntityFrameworkCore.DependencyInjection;
using TickerQ.Utilities;
using TickerQ.Utilities.Entities;

namespace APITemplate.Api.Extensions;

/// <summary>
/// Presentation-layer extension class that registers TickerQ-backed recurring background jobs,
/// per-entity soft-delete cleanup strategies, and related infrastructure services.
/// </summary>
public static class BackgroundJobsServiceCollectionExtensions
{
    private static readonly Type[] SoftDeleteCleanupOrder =
    [
        typeof(ProductDataLink),
        typeof(ProductReview),
        typeof(Product),
        typeof(AppUser),
        typeof(TenantInvitation),
        typeof(Category),
        typeof(Tenant),
    ];

    /// <summary>
    /// Registers background job services, soft-delete cleanup strategies, and the TickerQ
    /// runtime (when enabled), including its EF Core scheduler store and recurring job registrations.
    /// </summary>
    public static IServiceCollection AddBackgroundJobs(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddSingleton<
            IValidateOptions<BackgroundJobsOptions>,
            BackgroundJobsOptionsValidator
        >();
        services.AddValidatedOptions<BackgroundJobsOptions>(
            configuration,
            validateDataAnnotations: false
        );
        var options =
            configuration.SectionFor<BackgroundJobsOptions>().Get<BackgroundJobsOptions>()
            ?? new BackgroundJobsOptions();

        services.AddScoped<IFailedEmailRepository, FailedEmailRepository>();

        services.AddScoped<ICleanupService, CleanupService>();
        services.AddScoped<IReindexService, ReindexService>();
        services.AddScoped<IEmailRetryService, EmailRetryService>();
        services.AddScoped<
            IExternalIntegrationSyncService,
            ExternalIntegrationSyncServicePreview
        >();

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
        {
            return;
        }

        var dragonflyConnectionString = configuration
            .SectionFor<DragonflyOptions>()
            .GetValue<string>(nameof(DragonflyOptions.ConnectionString));

        if (string.IsNullOrWhiteSpace(dragonflyConnectionString))
        {
            throw new InvalidOperationException(
                "Background jobs require Dragonfly:ConnectionString when BackgroundJobs:TickerQ:Enabled is true."
            );
        }

        if (
            !string.Equals(
                options.TickerQ.CoordinationConnection,
                TickerQSchedulerOptions.DefaultCoordinationConnection,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            throw new InvalidOperationException(
                $"Only '{TickerQSchedulerOptions.DefaultCoordinationConnection}' is supported for BackgroundJobs:TickerQ:CoordinationConnection."
            );
        }

        var connectionString = configuration.GetConnectionString(
            ConfigurationSections.DefaultConnection
        )!;
        var schemaName = TickerQSchedulerOptions.DefaultSchemaName;

        services.AddDbContext<TickerQSchedulerDbContext>(dbOptions =>
            dbOptions.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schemaName)
            )
        );
        services.AddScoped<TickerQRecurringJobRegistrar>();
        services.AddSingleton<IDistributedJobCoordinator, DragonflyDistributedJobCoordinator>();
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
        var softDeletableTypes = typeof(ISoftDeletable)
            .Assembly.GetTypes()
            .Where(t =>
                t is { IsClass: true, IsAbstract: false }
                && typeof(ISoftDeletable).IsAssignableFrom(t)
            )
            .OrderBy(GetSoftDeleteCleanupOrder)
            .ThenBy(t => t.Name);

        foreach (var entityType in softDeletableTypes)
        {
            var strategyType = typeof(SoftDeleteCleanupStrategy<>).MakeGenericType(entityType);
            services.AddScoped(typeof(ISoftDeleteCleanupStrategy), strategyType);
        }
    }

    private static int GetSoftDeleteCleanupOrder(Type entityType)
    {
        var index = Array.IndexOf(SoftDeleteCleanupOrder, entityType);
        return index >= 0 ? index : int.MaxValue;
    }
}
