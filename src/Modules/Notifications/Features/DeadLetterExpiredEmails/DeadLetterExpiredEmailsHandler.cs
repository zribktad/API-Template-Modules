using Notifications.Contracts;
using SharedKernel.Contracts.Commands.Email;

namespace Notifications.Features;

public sealed class DeadLetterExpiredEmailsHandler
{
    public static Task HandleAsync(
        DeadLetterExpiredEmailsCommand command,
        IEmailRetryService emailRetryService,
        CancellationToken ct
    ) =>
        emailRetryService.DeadLetterExpiredAsync(
            command.DeadLetterAfterHours,
            command.BatchSize,
            command.ClaimLeaseMinutes,
            ct
        );
}
