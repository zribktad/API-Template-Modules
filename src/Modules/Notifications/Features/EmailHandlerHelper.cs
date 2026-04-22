using Microsoft.Extensions.Logging;
using Notifications.Logging;
using SharedKernel.Application.Errors;

namespace Notifications.Features;

internal static class EmailHandlerHelper
{
    internal static void ThrowIfRenderFailed(
        ErrorOr<string> result,
        string templateName,
        ILogger logger
    )
    {
        if (!result.IsError)
            return;

        logger.EmailTemplateRenderFailed(
            templateName,
            result.FirstError.Code,
            result.FirstError.Description
        );
        throw new AppException(result.FirstError.Description, result.FirstError.Code);
    }
}
