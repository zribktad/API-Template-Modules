using BackgroundJobs.Validation;
using Microsoft.Extensions.Options;
using BackgroundJobs.Options;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.BackgroundJobs;

public sealed class BackgroundJobsOptionsValidatorTests
{
    private readonly BackgroundJobsOptionsValidator _sut = new();

    [Fact]
    public void Validate_WhenTickerQEnabledAndMissingPrefix_Fails()
    {
        BackgroundJobsOptions options = new()
        {
            TickerQ = new TickerQSchedulerOptions { Enabled = true, InstanceNamePrefix = "   " },
        };

        ValidateOptionsResult result = _sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("InstanceNamePrefix");
    }

    [Theory]
    [InlineData("not valid cron")]
    [InlineData("* * *")]
    public void Validate_WhenCleanupEnabledAndCronInvalid_Fails(string cron)
    {
        BackgroundJobsOptions options = new()
        {
            Cleanup = new CleanupJobOptions { Enabled = true, Cron = cron },
        };

        ValidateOptionsResult result = _sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("CRON");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-3)]
    public void Validate_WhenCleanupEnabledAndNegativeRetention_Fails(int days)
    {
        BackgroundJobsOptions options = new()
        {
            Cleanup = new CleanupJobOptions { Enabled = true, SoftDeleteRetentionDays = days },
        };

        ValidateOptionsResult result = _sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WhenEmailRetryEnabledAndBatchSizeZero_Fails()
    {
        BackgroundJobsOptions options = new()
        {
            EmailRetry = new EmailRetryJobOptions { Enabled = true, BatchSize = 0 },
        };

        ValidateOptionsResult result = _sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WhenDefaultsAndJobsDisabled_Succeeds()
    {
        BackgroundJobsOptions options = new();

        ValidateOptionsResult result = _sut.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }
}