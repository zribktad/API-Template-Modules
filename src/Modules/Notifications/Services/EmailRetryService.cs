using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Contracts;
using Notifications.Domain;
using Notifications.Logging;
using Polly;
using Polly.Registry;
using SharedKernel.Application.Options;
using SharedKernel.Application.Resilience;
using SharedKernel.Domain.Interfaces;

namespace Notifications.Services;

/// <summary>
///     Infrastructure implementation of <see cref="IEmailRetryService" /> that claims and retries
///     failed emails from the store and moves permanently undeliverable ones to the dead-letter state.
///     Uses optimistic per-record claiming to avoid duplicate processing in multi-instance deployments.
/// </summary>
public sealed class EmailRetryService : IEmailRetryService
{
    private readonly string _claimOwner;
    private readonly ILogger<EmailRetryService> _logger;
    private readonly EmailRetryJobOptions _options;
    private readonly IFailedEmailRepository _repository;
    private readonly ResiliencePipelineProvider<string> _resiliencePipelineProvider;
    private readonly IEmailSender _sender;
    private readonly TimeProvider _timeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public EmailRetryService(
        IFailedEmailRepository repository,
        IEmailSender sender,
        IUnitOfWork<NotificationsDbMarker> unitOfWork,
        TimeProvider timeProvider,
        IOptions<EmailRetryJobOptions> options,
        ResiliencePipelineProvider<string> resiliencePipelineProvider,
        ILogger<EmailRetryService> logger
    )
    {
        _claimOwner = $"{Environment.MachineName}:{Environment.ProcessId}";
        _repository = repository;
        _sender = sender;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _options = options.Value;
        _resiliencePipelineProvider = resiliencePipelineProvider;
        _logger = logger;
    }

    /// <summary>
    ///     Claims up to <paramref name="batchSize" /> retryable failed emails, attempts delivery via
    ///     the resilience pipeline, and commits progress per-email to prevent duplicate sends on crash.
    ///     Failures increment <c>RetryCount</c> and release the claim for future attempts.
    /// </summary>
    public async Task RetryFailedEmailsAsync(
        int maxRetryAttempts,
        int batchSize,
        CancellationToken ct = default
    )
    {
        ResiliencePipeline pipeline = _resiliencePipelineProvider.GetPipeline(
            ResiliencePipelineKeys.SmtpSend
        );
        DateTime claimedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        DateTime claimUntilUtc = claimedAtUtc.AddMinutes(_options.ClaimLeaseMinutes);
        List<FailedEmail> emails = await _repository.ClaimRetryableBatchAsync(
            maxRetryAttempts,
            batchSize,
            _claimOwner,
            claimedAtUtc,
            claimUntilUtc,
            ct
        );

        foreach (FailedEmail email in emails)
        {
            bool stagedDeleteAfterSuccessfulSend = false;
            try
            {
                EmailMessage message = new(
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
                stagedDeleteAfterSuccessfulSend = true;
                _logger.EmailRetrySucceeded(email.To, email.RetryCount + 1);
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

                _logger.EmailRetryAttemptFailed(ex, email.RetryCount, email.To);
            }

            try
            {
                // Commit after each email to ensure durable progress — avoids duplicate sends on crash
                await _unitOfWork.CommitAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                _repository.ClearChangeTracker();
                if (stagedDeleteAfterSuccessfulSend)
                {
                    if (await _repository.ExistsByIdAsync(email.Id, ct))
                    {
                        _logger.EmailRetryDeleteConcurrencyAfterSend(email.To);
                        await _repository.DeleteByIdAsync(email.Id, ct);
                    }
                }
                else
                {
                    _logger.EmailRetryCommitConcurrencyConflict(email.To);
                }
            }
        }
    }

    /// <summary>
    ///     Claims and marks as dead-lettered any failed emails that have been retrying for longer than
    ///     <paramref name="deadLetterAfterHours" /> hours, processing in batches until none remain.
    /// </summary>
    public async Task DeadLetterExpiredAsync(
        int deadLetterAfterHours,
        int batchSize,
        CancellationToken ct = default
    )
    {
        DateTime cutoff = _timeProvider.GetUtcNow().UtcDateTime.AddHours(-deadLetterAfterHours);
        int processed;

        do
        {
            DateTime claimedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
            List<FailedEmail> expired = await _repository.ClaimExpiredBatchAsync(
                cutoff,
                batchSize,
                _claimOwner,
                claimedAtUtc,
                claimedAtUtc.AddMinutes(_options.ClaimLeaseMinutes),
                ct
            );
            processed = expired.Count;

            foreach (FailedEmail email in expired)
            {
                ApplyDeadLetterTransition(email);
                await _repository.UpdateAsync(email, ct);

                _logger.EmailDeadLettered(email.To, email.Subject, deadLetterAfterHours);
            }

            if (processed > 0)
            {
                try
                {
                    await _unitOfWork.CommitAsync(ct);
                }
                catch (DbUpdateConcurrencyException)
                {
                    _repository.ClearChangeTracker();
                    _logger.EmailDeadLetterCommitConcurrencyConflict();
                    await ReplayDeadLetterBatchAfterConcurrencyAsync(expired, ct);
                }
            }
        } while (processed == batchSize);
    }

    private static void ApplyDeadLetterTransition(FailedEmail email)
    {
        email.IsDeadLettered = true;
        email.ClaimedBy = null;
        email.ClaimedAtUtc = null;
        email.ClaimedUntilUtc = null;
    }

    private async Task ReplayDeadLetterBatchAfterConcurrencyAsync(
        List<FailedEmail> claimedBatch,
        CancellationToken ct
    )
    {
        foreach (FailedEmail snapshot in claimedBatch)
        {
            FailedEmail? fresh = await _repository.FindTrackedByIdAsync(snapshot.Id, ct);
            if (fresh is null || fresh.IsDeadLettered)
                continue;

            ApplyDeadLetterTransition(fresh);
            await _repository.UpdateAsync(fresh, ct);
        }

        await _unitOfWork.CommitAsync(ct);
    }
}
