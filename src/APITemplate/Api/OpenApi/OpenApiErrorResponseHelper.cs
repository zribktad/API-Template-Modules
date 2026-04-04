using Microsoft.AspNetCore.WebUtilities;
using Microsoft.OpenApi;

namespace APITemplate.Api.OpenApi;

/// <summary>
///     Shared helper for adding RFC 7807 <c>application/problem+json</c> error response entries
///     to OpenAPI operations without duplicating the wiring across multiple transformers.
/// </summary>
internal static class OpenApiErrorResponseHelper
{
    /// <summary>
    ///     Adds an error response for <paramref name="statusCode" /> to the operation if one is not already present.
    ///     Uses the HTTP reason phrase as the description when <paramref name="description" /> is not supplied.
    /// </summary>
    internal static void AddErrorResponse(
        OpenApiOperation operation,
        int statusCode,
        IOpenApiSchema? schema = null,
        string? description = null
    )
    {
        string statusCodeKey = statusCode.ToString();
        operation.Responses ??= new OpenApiResponses();

        string resolvedDescription = string.IsNullOrWhiteSpace(description)
            ? ReasonPhrases.GetReasonPhrase(statusCode)
            : description;

        if (operation.Responses.TryGetValue(statusCodeKey, out IOpenApiResponse? existingBase))
        {
            if (schema is not null && existingBase is OpenApiResponse existing)
            {
                existing.Content ??= new Dictionary<string, OpenApiMediaType>();
                if (
                    existing.Content.TryGetValue(
                        "application/problem+json",
                        out OpenApiMediaType? mediaType
                    )
                )
                    mediaType.Schema ??= schema;
                else
                {
                    existing.Content["application/problem+json"] = new OpenApiMediaType
                    {
                        Schema = schema,
                    };
                }
            }

            return;
        }

        OpenApiResponse response = new() { Description = resolvedDescription };
        if (schema is not null)
        {
            response.Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/problem+json"] = new() { Schema = schema },
            };
        }

        operation.Responses[statusCodeKey] = response;
    }
}
