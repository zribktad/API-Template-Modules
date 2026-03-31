using Microsoft.AspNetCore.Mvc;

namespace APITemplate.Api.ExceptionHandling;

public static class ApiProblemDetailsOptions
{
    public static void Configure(ProblemDetailsOptions options)
    {
        options.CustomizeProblemDetails = context =>
        {
            var extensions = context.ProblemDetails.Extensions;
            extensions["traceId"] = context.HttpContext.TraceIdentifier;

            var errorCode =
                extensions.TryGetValue("errorCode", out var existingErrorCode)
                && existingErrorCode is string existing
                    ? existing
                    : ErrorCatalog.General.Unknown;

            extensions["errorCode"] = errorCode;
            context.ProblemDetails.Type ??= $"https://api-template.local/errors/{errorCode}";
        };
    }
}
