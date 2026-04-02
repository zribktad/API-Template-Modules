using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace APITemplate.Api.OpenApi;

/// <summary>
/// Improved alternative to repeating <c>[ProducesResponseType(typeof(ProblemDetails), 400/404/500)]</c>
/// on every controller action. This transformer adds ProblemDetails responses globally
/// and avoids duplication across individual controllers.
/// </summary>
public sealed class ProblemDetailsOpenApiTransformer : IOpenApiDocumentTransformer
{
    /// <summary>
    /// Registers the shared <c>ApiProblemDetails</c> schema component and attaches standardized
    /// error responses (400, 401, 403, 404, 409, 500) to every operation in the document.
    /// </summary>
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        document.Components ??= new OpenApiComponents();
        document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();

        var problemDetailsSchema = BuildProblemDetailsSchema();
        document.Components.Schemas["ApiProblemDetails"] = problemDetailsSchema;

        foreach (var pathEntry in document.Paths)
        {
            var path = pathEntry.Value;
            if (path.Operations is null)
                continue;

            foreach (var operation in path.Operations.Values)
            {
                int[] errorStatusCodes =
                [
                    StatusCodes.Status400BadRequest,
                    StatusCodes.Status401Unauthorized,
                    StatusCodes.Status403Forbidden,
                    StatusCodes.Status404NotFound,
                    StatusCodes.Status409Conflict,
                    StatusCodes.Status500InternalServerError,
                ];
                foreach (var statusCode in errorStatusCodes)
                    OpenApiErrorResponseHelper.AddErrorResponse(
                        operation,
                        statusCode,
                        problemDetailsSchema
                    );
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Builds the reusable OpenAPI schema for the RFC 7807 ProblemDetails response payload,
    /// including the custom <c>traceId</c>, <c>errorCode</c>, and <c>metadata</c> extensions.
    /// </summary>
    private static IOpenApiSchema BuildProblemDetailsSchema()
    {
        return new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Description = "Standard RFC 7807 ProblemDetails payload used by REST error responses.",
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["type"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Description = "Error documentation URI.",
                },
                ["title"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["status"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
                ["detail"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["instance"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["traceId"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["errorCode"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["metadata"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object | JsonSchemaType.Null,
                    AdditionalProperties = new OpenApiSchema
                    {
                        Type =
                            JsonSchemaType.String
                            | JsonSchemaType.Integer
                            | JsonSchemaType.Number
                            | JsonSchemaType.Boolean
                            | JsonSchemaType.Null
                            | JsonSchemaType.Object
                            | JsonSchemaType.Array,
                    },
                },
            },
            Required = new HashSet<string> { "type", "title", "status", "traceId", "errorCode" },
        };
    }
}
