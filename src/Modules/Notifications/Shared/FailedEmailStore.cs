using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Shared;
using Notifications.Logging;
using SharedKernel.Application.Options.BackgroundJobs;
using SharedKernel.Domain.Interfaces;

namespace Notifications.Shared;

/// <summary>
/// Infrastructure implementation of <see cref="IFailedEmailStore"/> that persists a <see cref="FailedEmail"/>
/// record when delivery fails, provided the email is marked retryable and the email-retry job is enabled.
/// Uses a new DI scope per call to avoid captive-dependency issues with scoped services.
/// </summary>
public sealed class FailedEmailStore : IFailedEmailStore
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly bool _enabled;
    private readonly ILogger<FailedEmailStore> _logger;

    public FailedEmailStore(
        IServiceScopeFactory scopeFactory,
        IOptions<BackgroundJobsOptions> options,
        ILogger<FailedEmailStore> logger
    )
    {
        _scopeFactory = scopeFactory;
        _enabled = options.Value.EmailRetry.Enabled;
        _logger = logger;
    }

    /// <summary>
    /// Persists a new <see cref="FailedEmail"/> for <paramref name="message"/> if the message is
    /// retryable and the email-retry feature is enabled; silently swallows storage errors to avoid
    /// masking the original send failure.
    /// </summary>
    public async Task StoreFailedAsync(
        EmailMessage message,
        string error,
        CancellationToken ct = default
    )
    {
        if (!_enabled || !message.Retryable)
        {
            return;
        }

        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            IFailedEmailRepository repository =
                scope.ServiceProvider.GetRequiredService<IFailedEmailRepository>();
            IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            TimeProvider timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

            var failedEmail = new FailedEmail
            {
                Id = Guid.NewGuid(),
                To = message.To,
                Subject = message.Subject,
                HtmlBody = message.HtmlBody,
                RetryCount = 0,
                CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
                LastError = FailedEmailErrorNormalizer.Normalize(error),
                TemplateName = message.TemplateName,
                ClaimedBy = null,
                ClaimedAtUtc = null,
                ClaimedUntilUtc = null,
            };

            await repository.AddAsync(failedEmail, ct);
            await unitOfWork.CommitAsync(ct);

            _logger.FailedEmailStored(message.To, message.Subject);
        }
        catch (Exception ex)
        {
            _logger.FailedEmailStorageError(ex, message.To);
        }
    }
}



