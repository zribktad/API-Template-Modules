using Microsoft.AspNetCore.WebUtilities;
using Microsoft.OpenApi;

namespace APITemplate.Api.OpenApi;

/// <summary>
/// Shared helper for adding RFC 7807 <c>application/problem+json</c> error response entries
/// to OpenAPI operations without duplicating the wiring across multiple transformers.
/// </summary>
internal static class OpenApiErrorResponseHelper
{
    /// <summary>
    /// Adds an error response for <paramref name="statusCode"/> to the operation if one is not already present.
    /// Uses the HTTP reason phrase as the description when <paramref name="description"/> is not supplied.
    /// </summary>
    internal static void AddErrorResponse(
        OpenApiOperation operation,
        int statusCode,
        IOpenApiSchema? schema = null,
        string? description = null
    )
    {
        var statusCodeKey = statusCode.ToString();
        operation.Responses ??= new OpenApiResponses();
        if (operation.Responses.ContainsKey(statusCodeKey))
            return;

        var resolvedDescription = string.IsNullOrWhiteSpace(description)
            ? ReasonPhrases.GetReasonPhrase(statusCode)
            : description;

        operation.Responses[statusCodeKey] = new OpenApiResponse
        {
            Description = resolvedDescription,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/problem+json"] = new OpenApiMediaType { Schema = schema },
            },
        };
    }
}
