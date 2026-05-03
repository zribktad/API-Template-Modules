using BackgroundJobs.Options;
using BackgroundJobs.TickerQ.RecurringJobRegistrations;
using BuildingBlocks.Application.BackgroundJobs;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.BackgroundJobs.Jobs;

[Trait("Category", "Unit")]
public sealed class RecurringJobRegistrationTests
{
    [Fact]
    public void ReindexRegistration_BuildsDefinitionFromInjectedOptionsWithoutServiceProvider()
    {
        BackgroundJobsOptions options = new()
        {
            Reindex = new ReindexJobOptions { Cron = "0 1 * * *", Enabled = true },
        };
        ReindexRecurringJobRegistration sut = new(Options.Create(options));

        RecurringBackgroundJobDefinition definition = sut.Build();

        definition.Id.ShouldBe(Guid.Parse("9cf4e6ef-a2dd-4ff7-8968-174a6236a59f"));
        definition.FunctionName.ShouldBe("reindex-recurring-job");
        definition.CronExpression.ShouldBe("0 1 * * *");
        definition.Enabled.ShouldBeTrue();
    }
}
