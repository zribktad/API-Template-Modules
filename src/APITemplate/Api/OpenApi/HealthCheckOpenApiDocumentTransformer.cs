using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using SharedKernel.Infrastructure.Health;

namespace APITemplate.Api.OpenApi;

/// <summary>
///     OpenAPI document transformer that manually registers health check endpoints in the
///     generated specification, since health-check endpoints are not discovered automatically by ASP.NET Core OpenAPI.
/// </summary>
public sealed class HealthCheckOpenApiDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        document.Paths ??= new OpenApiPaths();

        foreach (
            HealthCheckEndpointDefinition endpoint in HealthCheckEndpointConfiguration.Endpoints
        )
        {
            OpenApiPathItem pathItem = new();
            pathItem.AddOperation(
                HttpMethod.Get,
                new OpenApiOperation
                {
                    Tags = new HashSet<OpenApiTagReference>
                    {
                        new(HealthCheckEndpointConfiguration.OpenApiTag, document),
                    },
                    Summary = endpoint.Summary,
                    Description = endpoint.Description,
                    Responses = new OpenApiResponses
                    {
                        [StatusCodes.Status200OK.ToString()] = new OpenApiResponse
                        {
                            Description = "Healthy",
                        },
                        [StatusCodes.Status503ServiceUnavailable.ToString()] = new OpenApiResponse
                        {
                            Description = "Unhealthy - one or more services are down",
                        },
                    },
                }
            );
            document.Paths[endpoint.Path] = pathItem;
        }

        return Task.CompletedTask;
    }
}
