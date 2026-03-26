using APITemplate.Application.Common.Options;
using APITemplate.Infrastructure.BackgroundJobs.Validation;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.BackgroundJobs;

public sealed class BackgroundJobsOptionsValidatorTests
{
    [Fact]
    public void Validate_ReturnsSuccess_WhenDisabledJobsHaveInvalidValues()
    {
        var sut = new BackgroundJobsOptionsValidator();
        var options = new BackgroundJobsOptions
        {
            Cleanup = new CleanupJobOptions
            {
                Enabled = false,
                Cron = "",
                BatchSize = 0,
                ExpiredInvitationRetentionHours = -1,
                SoftDeleteRetentionDays = -1,
                OrphanedProductDataRetentionDays = -1,
            },
            Reindex = new ReindexJobOptions { Enabled = false, Cron = "" },
            ExternalSync = new ExternalSyncJobOptions { Enabled = false, Cron = "" },
            EmailRetry = new EmailRetryJobOptions
            {
                Enabled = false,
                Cron = "",
                BatchSize = 0,
                MaxRetryAttempts = 0,
                ClaimLeaseMinutes = 0,
                DeadLetterAfterHours = -1,
            },
        };

        var result = sut.Validate(name: null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ReturnsFailure_WhenEnabledCleanupHasInvalidValues()
    {
        var sut = new BackgroundJobsOptionsValidator();
        var options = new BackgroundJobsOptions
        {
            Cleanup = new CleanupJobOptions
            {
                Enabled = true,
                Cron = "",
                BatchSize = 0,
            },
        };

        var result = sut.Validate(name: null, options);

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain("BackgroundJobs:Cleanup:Cron is required.");
        result.Failures.ShouldContain("BackgroundJobs:Cleanup:BatchSize must be greater than 0.");
    }
}
