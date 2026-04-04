namespace APITemplate.Api.ExceptionHandling;

public static class ApiProblemDetailsOptions
{
    public static void Configure(ProblemDetailsOptions options)
    {
        options.CustomizeProblemDetails = context =>
        {
            IDictionary<string, object?> extensions = context.ProblemDetails.Extensions;
            extensions["traceId"] = context.HttpContext.TraceIdentifier;

            string errorCode =
                extensions.TryGetValue("errorCode", out object? existingErrorCode)
                && existingErrorCode is string existing
                    ? existing
                    : ErrorCatalog.General.Unknown;

            extensions["errorCode"] = errorCode;
            context.ProblemDetails.Type ??= $"https://api-template.local/errors/{errorCode}";
        };
    }
}
