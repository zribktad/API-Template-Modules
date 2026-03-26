using APITemplate.Application.Common.Options;
using Microsoft.Extensions.Options;
using NCrontab;

namespace APITemplate.Infrastructure.BackgroundJobs.Validation;

/// <summary>
/// Validates <see cref="BackgroundJobsOptions"/> at startup, ensuring all enabled jobs have
/// well-formed cron expressions and positive numeric settings before the application starts accepting traffic.
/// </summary>
public sealed class BackgroundJobsOptionsValidator : IValidateOptions<BackgroundJobsOptions>
{
    /// <summary>
    /// Validates all sub-option groups; returns a combined failure result if any setting is invalid,
    /// or <see cref="ValidateOptionsResult.Success"/> if all are well-formed.
    /// </summary>
    public ValidateOptionsResult Validate(string? name, BackgroundJobsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        ValidateTickerQ(options.TickerQ, failures);
        ValidateCleanup(options.Cleanup, failures);
        ValidateReindex(options.Reindex, failures);
        ValidateExternalSync(options.ExternalSync, failures);
        ValidateEmailRetry(options.EmailRetry, failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateTickerQ(TickerQSchedulerOptions options, List<string> failures)
    {
        if (!options.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.InstanceNamePrefix))
        {
            failures.Add("BackgroundJobs:TickerQ:InstanceNamePrefix is required.");
        }

        if (
            !string.Equals(
                options.CoordinationConnection,
                TickerQSchedulerOptions.DefaultCoordinationConnection,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            failures.Add(
                $"BackgroundJobs:TickerQ:CoordinationConnection must be '{TickerQSchedulerOptions.DefaultCoordinationConnection}'."
            );
        }
    }

    private static void ValidateCleanup(CleanupJobOptions options, List<string> failures)
    {
        if (!options.Enabled)
        {
            return;
        }

        ValidateCron("BackgroundJobs:Cleanup:Cron", options.Cron, failures);
        ValidatePositive("BackgroundJobs:Cleanup:BatchSize", options.BatchSize, failures);
        ValidateNonNegative(
            "BackgroundJobs:Cleanup:ExpiredInvitationRetentionHours",
            options.ExpiredInvitationRetentionHours,
            failures
        );
        ValidateNonNegative(
            "BackgroundJobs:Cleanup:SoftDeleteRetentionDays",
            options.SoftDeleteRetentionDays,
            failures
        );
        ValidateNonNegative(
            "BackgroundJobs:Cleanup:OrphanedProductDataRetentionDays",
            options.OrphanedProductDataRetentionDays,
            failures
        );
    }

    private static void ValidateReindex(ReindexJobOptions options, List<string> failures)
    {
        if (!options.Enabled)
        {
            return;
        }

        ValidateCron("BackgroundJobs:Reindex:Cron", options.Cron, failures);
    }

    private static void ValidateExternalSync(ExternalSyncJobOptions options, List<string> failures)
    {
        if (!options.Enabled)
        {
            return;
        }

        ValidateCron("BackgroundJobs:ExternalSync:Cron", options.Cron, failures);
    }

    private static void ValidateEmailRetry(EmailRetryJobOptions options, List<string> failures)
    {
        if (!options.Enabled)
        {
            return;
        }

        ValidateCron("BackgroundJobs:EmailRetry:Cron", options.Cron, failures);
        ValidatePositive("BackgroundJobs:EmailRetry:BatchSize", options.BatchSize, failures);
        ValidatePositive(
            "BackgroundJobs:EmailRetry:MaxRetryAttempts",
            options.MaxRetryAttempts,
            failures
        );
        ValidatePositive(
            "BackgroundJobs:EmailRetry:ClaimLeaseMinutes",
            options.ClaimLeaseMinutes,
            failures
        );
        ValidateNonNegative(
            "BackgroundJobs:EmailRetry:DeadLetterAfterHours",
            options.DeadLetterAfterHours,
            failures
        );
    }

    private static void ValidateCron(string path, string cron, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(cron))
        {
            failures.Add($"{path} is required.");
            return;
        }

        try
        {
            CrontabSchedule.Parse(cron);
        }
        catch (CrontabException)
        {
            failures.Add($"{path} must be a valid 5-part CRON expression.");
        }
    }

    private static void ValidatePositive(string path, int value, List<string> failures)
    {
        if (value <= 0)
        {
            failures.Add($"{path} must be greater than 0.");
        }
    }

    private static void ValidateNonNegative(string path, int value, List<string> failures)
    {
        if (value < 0)
        {
            failures.Add($"{path} must be greater than or equal to 0.");
        }
    }
}
