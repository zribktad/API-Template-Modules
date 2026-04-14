using Notifications.Contracts;
using SharedKernel.Contracts.Commands.Email;

namespace Notifications.Features;

public sealed class RetryFailedEmailsHandler
{
    public static Task HandleAsync(
        RetryFailedEmailsCommand command,
        IEmailRetryService emailRetryService,
        CancellationToken ct
    ) =>
        emailRetryService.RetryFailedEmailsAsync(
            command.MaxRetryAttempts,
            command.BatchSize,
            command.ClaimLeaseMinutes,
            ct
        );
}
