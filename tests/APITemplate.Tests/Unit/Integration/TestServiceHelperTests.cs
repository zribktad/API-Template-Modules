using APITemplate.Api.Extensions;
using APITemplate.Application.Common.BackgroundJobs;
using APITemplate.Application.Common.Options;
using APITemplate.Infrastructure.BackgroundJobs.Services;
using APITemplate.Infrastructure.BackgroundJobs.TickerQ;
using APITemplate.Infrastructure.BackgroundJobs.TickerQ.Coordination;
using APITemplate.Tests.Integration.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Integration;

public sealed class TestServiceHelperTests
{
    [Fact]
    public void RemoveTickerQRuntimeServices_RemovesSchedulerRuntimeButKeepsCoreBackgroundJobServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBackgroundJobs(BuildConfiguration());

        TestServiceHelper.RemoveTickerQRuntimeServices(services);

        services.ShouldNotContain(x => x.ServiceType == typeof(TickerQSchedulerDbContext));
        services.ShouldNotContain(x => x.ServiceType == typeof(TickerQRecurringJobRegistrar));
        services.ShouldNotContain(x => x.ServiceType == typeof(IDistributedJobCoordinator));
        services.ShouldNotContain(x =>
            x.ServiceType == typeof(IRecurringBackgroundJobRegistration)
        );
        services
            .Any(x =>
                x.ServiceType.Namespace is string typeNamespace
                && typeNamespace.StartsWith("TickerQ", StringComparison.Ordinal)
            )
            .ShouldBeFalse();

        services.ShouldContain(x => x.ServiceType == typeof(ICleanupService));
        services.ShouldContain(x => x.ServiceType == typeof(IReindexService));
        services.ShouldContain(x => x.ServiceType == typeof(IEmailRetryService));
    }

    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Host=localhost;Database=test;Username=test;Password=test",
                    ["Dragonfly:ConnectionString"] = "localhost:6379",
                    ["BackgroundJobs:TickerQ:Enabled"] = "true",
                    ["BackgroundJobs:Cleanup:Cron"] = "0 * * * *",
                    ["BackgroundJobs:Reindex:Cron"] = "0 */6 * * *",
                    ["BackgroundJobs:EmailRetry:Cron"] = "*/15 * * * *",
                    ["BackgroundJobs:ExternalSync:Cron"] = "0 */12 * * *",
                }
            )
            .Build();
}
