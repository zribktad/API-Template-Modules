using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;
using SharedKernel.Contracts.Api;

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
            // When ErrorTypeBaseUri is set, use our documentation URI (overrides host defaults).
            // When unset, leave Type unchanged so RFC 9110 defaults can apply.
            if (typeUri is not null)
                context.ProblemDetails.Type = typeUri;
        };
    }
}
