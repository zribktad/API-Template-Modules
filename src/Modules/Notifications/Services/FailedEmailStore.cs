using BuildingBlocks.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Notifications.Contracts;
using Notifications.Domain;
using Notifications.Logging;

namespace Notifications.Services;

/// <summary>
///     Infrastructure implementation of <see cref="IFailedEmailStore" /> that persists a <see cref="FailedEmail" />
///     record when delivery fails, provided the email is marked retryable.
///     Uses a new DI scope per call to avoid captive-dependency issues with scoped services.
/// </summary>
public sealed class FailedEmailStore : IFailedEmailStore
{
    private readonly ILogger<FailedEmailStore> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public FailedEmailStore(IServiceScopeFactory scopeFactory, ILogger<FailedEmailStore> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    ///     Persists a new <see cref="FailedEmail" /> for <paramref name="message" /> if the message is
    ///     retryable; silently swallows storage errors to avoid
    ///     masking the original send failure.
    /// </summary>
    public async Task StoreFailedAsync(
        EmailMessage message,
        string error,
        CancellationToken ct = default
    )
    {
        if (!message.Retryable)
            return;

        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            IFailedEmailRepository repository =
                scope.ServiceProvider.GetRequiredService<IFailedEmailRepository>();
            IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            TimeProvider timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

            FailedEmail failedEmail = FailedEmail.Create(
                message.To,
                message.Subject,
                message.HtmlBody,
                timeProvider,
                message.TemplateName,
                initialError: error
            );

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
