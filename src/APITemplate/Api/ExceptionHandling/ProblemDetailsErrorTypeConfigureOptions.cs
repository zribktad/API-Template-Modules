using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace APITemplate.Api.ExceptionHandling;

/// <summary>
///     Applies shared RFC 7807 customization for all <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails" />
///     written through <see cref="IProblemDetailsService" />: <c>traceId</c>, <c>errorCode</c>, and
///     absolute <c>type</c> URI from <see cref="ErrorDocumentationOptions" />.
/// </summary>
internal sealed class ProblemDetailsErrorTypeConfigureOptions
    : IConfigureOptions<ProblemDetailsOptions>
{
    private readonly IOptions<ErrorDocumentationOptions> _errorDocumentation;

    public ProblemDetailsErrorTypeConfigureOptions(
        IOptions<ErrorDocumentationOptions> errorDocumentation
    )
    {
        _errorDocumentation = errorDocumentation;
    }

    public void Configure(ProblemDetailsOptions options)
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

            string? typeUri = ProblemDetailsErrorTypeUri.BuildAbsoluteUri(
                _errorDocumentation.Value.ErrorTypeBaseUri,
                errorCode
            );
            // Do not use ??= : the host may already set a default RFC 9110 "type" for the status code.
            if (typeUri is not null)
                context.ProblemDetails.Type = typeUri;
        };
    }
}
