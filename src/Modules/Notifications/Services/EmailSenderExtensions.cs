using Notifications.Contracts;
using SharedKernel.Application.Errors;

namespace Notifications.Services;

internal static class EmailSenderExtensions
{
    internal static async ValueTask SendOrThrowAsync(
        this IEmailSender sender,
        EmailMessage message,
        CancellationToken ct
    )
    {
        ErrorOr<Success> result = await sender.SendAsync(message, ct);
        if (result.IsError)
            throw new AppException(result.FirstError.Description, result.FirstError.Code);
    }
}
