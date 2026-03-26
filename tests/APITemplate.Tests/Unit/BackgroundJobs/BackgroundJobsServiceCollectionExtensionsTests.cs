using APITemplate.Api.Extensions;
using APITemplate.Application.Common.BackgroundJobs;
using APITemplate.Application.Common.Email;
using APITemplate.Application.Common.Options;
using APITemplate.Domain.Entities;
using APITemplate.Infrastructure.BackgroundJobs.Services;
using APITemplate.Infrastructure.BackgroundJobs.TickerQ;
using APITemplate.Infrastructure.BackgroundJobs.TickerQ.Coordination;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.BackgroundJobs;

public sealed class BackgroundJobsServiceCollectionExtensionsTests
{
    [Fact]
    public void AddBackgroundJobs_WhenTickerQEnabled_RegistersTickerQInfrastructureAndRemovesLegacyHostedServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Database=test;Username=test;Password=test",
                ["Dragonfly:ConnectionString"] = "localhost:6379",
                ["BackgroundJobs:TickerQ:Enabled"] = "true",
                ["BackgroundJobs:Cleanup:Enabled"] = "true",
                ["BackgroundJobs:Cleanup:Cron"] = "0 * * * *",
                ["BackgroundJobs:Reindex:Enabled"] = "true",
                ["BackgroundJobs:Reindex:Cron"] = "0 */6 * * *",
                ["BackgroundJobs:EmailRetry:Enabled"] = "true",
                ["BackgroundJobs:EmailRetry:Cron"] = "*/15 * * * *",
            }
        );

        services.AddBackgroundJobs(configuration);

        services.ShouldContain(x => x.ServiceType == typeof(TickerQSchedulerDbContext));
        services.ShouldContain(x => x.ServiceType == typeof(TickerQRecurringJobRegistrar));
        services.ShouldContain(x => x.ServiceType == typeof(IDistributedJobCoordinator));
        services.ShouldContain(x => x.ServiceType == typeof(IExternalIntegrationSyncService));
        services
            .Count(x => x.ServiceType == typeof(IRecurringBackgroundJobRegistration))
            .ShouldBe(4);
        services.ShouldContain(x => x.ServiceType == typeof(IEmailRetryService));
        services.ShouldContain(x => x.ServiceType == typeof(ICleanupService));
        services.ShouldContain(x => x.ServiceType == typeof(IReindexService));
        services.ShouldNotContain(x =>
            x.ServiceType == typeof(IHostedService)
            && x.ImplementationType != null
            && (
                x.ImplementationType.Name == "CleanupBackgroundJob"
                || x.ImplementationType.Name == "ReindexBackgroundJob"
                || x.ImplementationType.Name == "EmailRetryBackgroundJob"
                || x.ImplementationType.Name == "PeriodicBackgroundJob"
            )
        );
    }

    [Fact]
    public void AddBackgroundJobs_WhenTickerQDisabled_DoesNotRegisterTickerQOnlyServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var configuration = BuildConfiguration(
            new Dictionary<string, string?> { ["BackgroundJobs:TickerQ:Enabled"] = "false" }
        );

        services.AddBackgroundJobs(configuration);

        services.ShouldNotContain(x => x.ServiceType == typeof(TickerQSchedulerDbContext));
        services.ShouldNotContain(x => x.ServiceType == typeof(TickerQRecurringJobRegistrar));
        services.ShouldNotContain(x => x.ServiceType == typeof(IDistributedJobCoordinator));
        services.ShouldContain(x => x.ServiceType == typeof(IFailedEmailRepository));
        services.ShouldContain(x => x.ServiceType == typeof(ICleanupService));
        services.ShouldContain(x => x.ServiceType == typeof(IReindexService));
        services.ShouldContain(x => x.ServiceType == typeof(IEmailRetryService));
    }

    [Fact]
    public void AddBackgroundJobs_WhenTickerQEnabledWithoutDragonflyConnection_Throws()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Database=test;Username=test;Password=test",
                ["BackgroundJobs:TickerQ:Enabled"] = "true",
            }
        );

        var ex = Should.Throw<InvalidOperationException>(() =>
            services.AddBackgroundJobs(configuration)
        );

        ex.Message.ShouldContain("Dragonfly:ConnectionString");
    }

    [Fact]
    public void AddBackgroundJobs_BindsTickerQOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Database=test;Username=test;Password=test",
                ["Dragonfly:ConnectionString"] = "localhost:6379",
                ["BackgroundJobs:TickerQ:Enabled"] = "true",
                ["BackgroundJobs:TickerQ:FailClosed"] = "true",
                ["BackgroundJobs:TickerQ:InstanceNamePrefix"] = "ApiTemplate",
                ["BackgroundJobs:TickerQ:CoordinationConnection"] = "Dragonfly",
                ["BackgroundJobs:ExternalSync:Cron"] = "0 */12 * * *",
                ["BackgroundJobs:Cleanup:Cron"] = "5 * * * *",
                ["BackgroundJobs:Reindex:Cron"] = "0 */4 * * *",
                ["BackgroundJobs:EmailRetry:Cron"] = "*/5 * * * *",
                ["BackgroundJobs:EmailRetry:ClaimLeaseMinutes"] = "9",
            }
        );

        services.AddBackgroundJobs(configuration);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<BackgroundJobsOptions>>().Value;

        options.TickerQ.Enabled.ShouldBeTrue();
        options.TickerQ.FailClosed.ShouldBeTrue();
        options.TickerQ.InstanceNamePrefix.ShouldBe("ApiTemplate");
        options.TickerQ.CoordinationConnection.ShouldBe("Dragonfly");
        options.ExternalSync.Cron.ShouldBe("0 */12 * * *");
        options.Cleanup.Cron.ShouldBe("5 * * * *");
        options.Reindex.Cron.ShouldBe("0 */4 * * *");
        options.EmailRetry.Cron.ShouldBe("*/5 * * * *");
        options.EmailRetry.ClaimLeaseMinutes.ShouldBe(9);
    }

    [Fact]
    public void AddBackgroundJobs_WhenCleanupBatchSizeIsZero_ThrowsOnOptionsAccess()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Database=test;Username=test;Password=test",
                ["Dragonfly:ConnectionString"] = "localhost:6379",
                ["BackgroundJobs:TickerQ:Enabled"] = "true",
                ["BackgroundJobs:Cleanup:Enabled"] = "true",
                ["BackgroundJobs:Cleanup:BatchSize"] = "0",
            }
        );

        services.AddBackgroundJobs(configuration);
        using var provider = services.BuildServiceProvider();

        var ex = Should.Throw<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<BackgroundJobsOptions>>().Value
        );

        ex.Failures.ShouldContain("BackgroundJobs:Cleanup:BatchSize must be greater than 0.");
    }

    [Fact]
    public void AddBackgroundJobs_WhenCronIsInvalid_ThrowsOnOptionsAccess()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Database=test;Username=test;Password=test",
                ["Dragonfly:ConnectionString"] = "localhost:6379",
                ["BackgroundJobs:TickerQ:Enabled"] = "true",
                ["BackgroundJobs:EmailRetry:Enabled"] = "true",
                ["BackgroundJobs:EmailRetry:Cron"] = "not-a-cron",
            }
        );

        services.AddBackgroundJobs(configuration);
        using var provider = services.BuildServiceProvider();

        var ex = Should.Throw<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<BackgroundJobsOptions>>().Value
        );

        ex.Failures.ShouldContain(
            "BackgroundJobs:EmailRetry:Cron must be a valid 5-part CRON expression."
        );
    }

    [Fact]
    public void AddBackgroundJobs_RegistersSoftDeleteCleanupStrategiesInDependencyOrder()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddBackgroundJobs(BuildConfiguration([]));

        var entityTypes = services
            .Where(x =>
                x.ServiceType == typeof(ISoftDeleteCleanupStrategy)
                && x.ImplementationType is not null
            )
            .Select(x => x.ImplementationType!.GenericTypeArguments.Single())
            .ToList();

        entityTypes
            .IndexOf(typeof(ProductDataLink))
            .ShouldBeLessThan(entityTypes.IndexOf(typeof(Product)));
        entityTypes
            .IndexOf(typeof(ProductReview))
            .ShouldBeLessThan(entityTypes.IndexOf(typeof(AppUser)));
        entityTypes.IndexOf(typeof(AppUser)).ShouldBeLessThan(entityTypes.IndexOf(typeof(Tenant)));
        entityTypes
            .IndexOf(typeof(TenantInvitation))
            .ShouldBeLessThan(entityTypes.IndexOf(typeof(Tenant)));
        entityTypes.IndexOf(typeof(Category)).ShouldBeLessThan(entityTypes.IndexOf(typeof(Tenant)));
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
