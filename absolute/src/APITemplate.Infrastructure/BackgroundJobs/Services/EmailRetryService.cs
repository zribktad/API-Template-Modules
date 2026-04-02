using APITemplate.Application.Common.BackgroundJobs;
using APITemplate.Application.Common.Email;
using APITemplate.Application.Common.Options;
using APITemplate.Application.Common.Resilience;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.Registry;

namespace APITemplate.Infrastructure.BackgroundJobs.Services;

/// <summary>
/// Infrastructure implementation of <see cref="IEmailRetryService"/> that claims and retries
/// failed emails from the store and moves permanently undeliverable ones to the dead-letter state.
/// Uses optimistic per-record claiming to avoid duplicate processing in multi-instance deployments.
/// </summary>
public sealed class EmailRetryService : IEmailRetryService
{
    private readonly string _claimOwner;
    private readonly IFailedEmailRepository _repository;
    private readonly IEmailSender _sender;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly EmailRetryJobOptions _options;
    private readonly ResiliencePipelineProvider<string> _resiliencePipelineProvider;
    private readonly ILogger<EmailRetryService> _logger;

    public EmailRetryService(
        IFailedEmailRepository repository,
        IEmailSender sender,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        IOptions<BackgroundJobsOptions> options,
        ResiliencePipelineProvider<string> resiliencePipelineProvider,
        ILogger<EmailRetryService> logger
    )
    {
        _claimOwner = $"{Environment.MachineName}:{Environment.ProcessId}";
        _repository = repository;
        _sender = sender;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _options = options.Value.EmailRetry;
        _resiliencePipelineProvider = resiliencePipelineProvider;
        _logger = logger;
    }

    /// <summary>
    /// Claims up to <paramref name="batchSize"/> retryable failed emails, attempts delivery via
    /// the resilience pipeline, and commits progress per-email to prevent duplicate sends on crash.
    /// Failures increment <c>RetryCount</c> and release the claim for future attempts.
    /// </summary>
    public async Task RetryFailedEmailsAsync(
        int maxRetryAttempts,
        int batchSize,
        CancellationToken ct = default
    )
    {
        var pipeline = _resiliencePipelineProvider.GetPipeline(ResiliencePipelineKeys.SmtpSend);
        var claimedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var claimUntilUtc = claimedAtUtc.AddMinutes(_options.ClaimLeaseMinutes);
        var emails = await _repository.ClaimRetryableBatchAsync(
            maxRetryAttempts,
            batchSize,
            _claimOwner,
            claimedAtUtc,
            claimUntilUtc,
            ct
        );

        foreach (var email in emails)
        {
            try
            {
                var message = new EmailMessage(
                    email.To,
                    email.Subject,
                    email.HtmlBody,
                    email.TemplateName
                );
                await pipeline.ExecuteAsync(
                    async token => await _sender.SendAsync(message, token),
                    ct
                );

                await _repository.DeleteAsync(email, ct);
                _logger.LogInformation(
                    "Successfully retried email to {Recipient} (attempt {Attempt}).",
                    email.To,
                    email.RetryCount + 1
                );
            }
            catch (Exception ex)
            {
                email.RetryCount++;
                email.LastAttemptAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
                email.LastError = FailedEmailErrorNormalizer.Normalize(ex.Message);
                email.ClaimedBy = null;
                email.ClaimedAtUtc = null;
                email.ClaimedUntilUtc = null;
                await _repository.UpdateAsync(email, ct);

                _logger.LogWarning(
                    ex,
                    "Retry attempt {Attempt} failed for email to {Recipient}.",
                    email.RetryCount,
                    email.To
                );
            }

            // Commit after each email to ensure durable progress — avoids duplicate sends on crash
            await _unitOfWork.CommitAsync(ct);
        }
    }

    /// <summary>
    /// Claims and marks as dead-lettered any failed emails that have been retrying for longer than
    /// <paramref name="deadLetterAfterHours"/> hours, processing in batches until none remain.
    /// </summary>
    public async Task DeadLetterExpiredAsync(
        int deadLetterAfterHours,
        int batchSize,
        CancellationToken ct = default
    )
    {
        var cutoff = _timeProvider.GetUtcNow().UtcDateTime.AddHours(-deadLetterAfterHours);
        int processed;

        do
        {
            var claimedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var expired = await _repository.ClaimExpiredBatchAsync(
                cutoff,
                batchSize,
                _claimOwner,
                claimedAtUtc,
                claimedAtUtc.AddMinutes(_options.ClaimLeaseMinutes),
                ct
            );
            processed = expired.Count;

            foreach (var email in expired)
            {
                email.IsDeadLettered = true;
                email.ClaimedBy = null;
                email.ClaimedAtUtc = null;
                email.ClaimedUntilUtc = null;
                await _repository.UpdateAsync(email, ct);

                _logger.LogWarning(
                    "Dead-lettered email to {Recipient} with subject '{Subject}' after {Hours}h.",
                    email.To,
                    email.Subject,
                    deadLetterAfterHours
                );
            }

            if (processed > 0)
            {
                await _unitOfWork.CommitAsync(ct);
            }
        } while (processed == batchSize);
    }
}
