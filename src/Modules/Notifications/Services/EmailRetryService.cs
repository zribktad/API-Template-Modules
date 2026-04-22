using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Notifications.Contracts;
using Notifications.Domain;
using Notifications.Logging;
using Polly;
using SharedKernel.Application.Errors;
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
    private readonly IFailedEmailRepository _repository;
    private readonly ISmtpSendPipelineProvider _smtpSendPipelineProvider;
    private readonly IEmailSender _sender;
    private readonly TimeProvider _timeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public EmailRetryService(
        IFailedEmailRepository repository,
        IEmailSender sender,
        IUnitOfWork<NotificationsDbMarker> unitOfWork,
        TimeProvider timeProvider,
        ISmtpSendPipelineProvider smtpSendPipelineProvider,
        ILogger<EmailRetryService> logger
    )
    {
        _claimOwner = $"{Environment.MachineName}:{Environment.ProcessId}";
        _repository = repository;
        _sender = sender;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _smtpSendPipelineProvider = smtpSendPipelineProvider;
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
        int claimLeaseMinutes,
        CancellationToken ct = default
    )
    {
        ResiliencePipeline pipeline = _smtpSendPipelineProvider.Get();
        DateTime claimedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        DateTime claimUntilUtc = claimedAtUtc.AddMinutes(claimLeaseMinutes);
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
                await pipeline.ExecuteAsync(token => _sender.SendOrThrowAsync(message, token), ct);

                await _repository.DeleteAsync(email, CancellationToken.None);
                stagedDeleteAfterSuccessfulSend = true;
                _logger.EmailRetrySucceeded(email.To, email.RetryCount + 1);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                email.RetryCount++;
                email.LastAttemptAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
                string rawError = ex is AppException ae
                    ? $"{ae.ErrorCode}: {ae.Message}"
                    : ex.Message;
                email.LastError = FailedEmailErrorNormalizer.Normalize(rawError);
                email.ClaimedBy = null;
                email.ClaimedAtUtc = null;
                email.ClaimedUntilUtc = null;
                await _repository.UpdateAsync(email, ct);

                _logger.EmailRetryAttemptFailed(ex, email.RetryCount, email.To);
            }

            try
            {
                // Commit after each email to ensure durable progress — avoids duplicate sends on crash
                // After the "point of no return" (successful SMTP), commit with None and surface OCE only to outer loop.
                await _unitOfWork.CommitAsync(
                    stagedDeleteAfterSuccessfulSend ? CancellationToken.None : ct
                );
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
        int claimLeaseMinutes,
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
                claimedAtUtc.AddMinutes(claimLeaseMinutes),
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
