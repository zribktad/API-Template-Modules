using Microsoft.AspNetCore.Http;

namespace APITemplate.Api.ExceptionHandling;

/// <summary>
/// Provides a static configuration helper that customizes the global
/// <see cref="ProblemDetailsOptions"/> for the API presentation layer.
/// </summary>
public static class ApiProblemDetailsOptions
{
    /// <summary>
    /// Configures global <see cref="ProblemDetails"/> enrichment for API responses.
    /// </summary>
    /// <remarks>
    /// Adds a request trace identifier, guarantees an <c>errorCode</c> fallback, and
    /// ensures a stable <c>type</c> URI shape for error documentation.
    /// </remarks>
    public static void Configure(ProblemDetailsOptions options)
    {
        options.CustomizeProblemDetails = context =>
        {
            var extensions = context.ProblemDetails.Extensions;
            extensions["traceId"] = context.HttpContext.TraceIdentifier;

            // Preserve errorCode set by upstream handlers; only fall back when not provided.
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
