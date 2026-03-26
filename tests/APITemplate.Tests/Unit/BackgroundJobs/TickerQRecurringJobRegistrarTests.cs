using APITemplate.Application.Common.BackgroundJobs;
using APITemplate.Application.Common.Options;
using APITemplate.Infrastructure.BackgroundJobs.TickerQ;
using APITemplate.Infrastructure.BackgroundJobs.TickerQ.RecurringJobRegistrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using TickerQ.Utilities.Entities;
using Xunit;

namespace APITemplate.Tests.Unit.BackgroundJobs;

public sealed class TickerQRecurringJobRegistrarTests
{
    [Fact]
    public async Task SyncAsync_SeedsAndUpdatesRecurringTickerDefinitionsWithoutDuplicates()
    {
        var ct = TestContext.Current.CancellationToken;
        var dbOptions = new DbContextOptionsBuilder<TickerQSchedulerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        var options = Options.Create(
            new BackgroundJobsOptions
            {
                ExternalSync = new ExternalSyncJobOptions
                {
                    Enabled = false,
                    Cron = "0 */12 * * *",
                },
                Cleanup = new CleanupJobOptions { Enabled = true, Cron = "0 * * * *" },
                Reindex = new ReindexJobOptions { Enabled = true, Cron = "0 */6 * * *" },
                EmailRetry = new EmailRetryJobOptions { Enabled = false, Cron = "*/15 * * * *" },
            }
        );

        await using var dbContext = new TickerQSchedulerDbContext(dbOptions);
        var registrations = CreateRegistrations();

        var registrar = new TickerQRecurringJobRegistrar(
            dbContext,
            registrations,
            options,
            TimeProvider.System,
            NullLogger<TickerQRecurringJobRegistrar>.Instance
        );

        await registrar.SyncAsync(ct);

        var seededTickers = await dbContext
            .Set<CronTickerEntity>()
            .OrderBy(x => x.Function)
            .ToListAsync(ct);
        seededTickers.Count.ShouldBe(4);
        seededTickers
            .Single(x => x.Function == TickerQFunctionNames.ExternalSync)
            .IsEnabled.ShouldBeFalse();
        seededTickers
            .Single(x => x.Function == TickerQFunctionNames.EmailRetry)
            .IsEnabled.ShouldBeFalse();

        var updatedOptions = Options.Create(
            new BackgroundJobsOptions
            {
                ExternalSync = new ExternalSyncJobOptions { Enabled = true, Cron = "0 */6 * * *" },
                Cleanup = new CleanupJobOptions { Enabled = true, Cron = "5 * * * *" },
                Reindex = new ReindexJobOptions { Enabled = true, Cron = "0 */6 * * *" },
                EmailRetry = new EmailRetryJobOptions { Enabled = true, Cron = "*/5 * * * *" },
            }
        );

        var updatedRegistrar = new TickerQRecurringJobRegistrar(
            dbContext,
            registrations,
            updatedOptions,
            TimeProvider.System,
            NullLogger<TickerQRecurringJobRegistrar>.Instance
        );

        await updatedRegistrar.SyncAsync(ct);

        var updatedTickers = await dbContext
            .Set<CronTickerEntity>()
            .OrderBy(x => x.Function)
            .ToListAsync(ct);
        updatedTickers.Count.ShouldBe(4);
        updatedTickers.Single(x => x.Id == TickerQJobIds.ExternalSync).IsEnabled.ShouldBeTrue();
        updatedTickers
            .Single(x => x.Id == TickerQJobIds.ExternalSync)
            .Expression.ShouldBe("0 */6 * * *");
        updatedTickers.Single(x => x.Id == TickerQJobIds.Cleanup).Expression.ShouldBe("5 * * * *");
        updatedTickers.Single(x => x.Id == TickerQJobIds.EmailRetry).IsEnabled.ShouldBeTrue();
        updatedTickers
            .Single(x => x.Id == TickerQJobIds.EmailRetry)
            .Expression.ShouldBe("*/5 * * * *");
    }

    private static IReadOnlyCollection<IRecurringBackgroundJobRegistration> CreateRegistrations() =>
        [
            new ExternalSyncRecurringJobRegistration(),
            new CleanupRecurringJobRegistration(),
            new ReindexRecurringJobRegistration(),
            new EmailRetryRecurringJobRegistration(),
        ];
}
