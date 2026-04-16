using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
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

            bool hasExplicitErrorCode =
                extensions.TryGetValue(
                    ProblemDetailsConstants.ErrorCode,
                    out object? existingErrorCode
                )
                && existingErrorCode is string existing
                && !string.IsNullOrWhiteSpace(existing);

            if (!hasExplicitErrorCode)
            {
                bool isValidationProblem =
                    context.ProblemDetails is HttpValidationProblemDetails
                    || extensions.ContainsKey(ProblemDetailsConstants.Errors);

                string errorCode = isValidationProblem
                    ? ErrorCatalog.General.ValidationFailed
                    : ErrorCatalog.General.Unknown;

                extensions[ProblemDetailsConstants.ErrorCode] = errorCode;
            }

            string errorCodeToUse = (string)extensions[ProblemDetailsConstants.ErrorCode]!;

            string? typeUri = ProblemDetailsErrorTypeUri.BuildAbsoluteUri(
                _errorDocumentation.Value.ErrorTypeBaseUri,
                errorCodeToUse
            );
            // When ErrorTypeBaseUri is set, use our documentation URI (overrides host defaults).
            // When unset, leave Type unchanged so RFC 9110 defaults can apply.
            if (typeUri is not null)
                context.ProblemDetails.Type = typeUri;
        };
    }
}
