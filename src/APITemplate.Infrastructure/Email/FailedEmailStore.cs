using APITemplate.Application.Common.Email;
using APITemplate.Application.Common.Options;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace APITemplate.Infrastructure.Email;

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
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IFailedEmailRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

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

            _logger.LogWarning(
                "Stored failed email to {Recipient} with subject '{Subject}' for retry.",
                message.To,
                message.Subject
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to store failed email to {Recipient} for retry.",
                message.To
            );
        }
    }
}
