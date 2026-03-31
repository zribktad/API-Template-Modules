using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace APITemplate.Api.OpenApi;

/// <summary>
/// Adds 401/403 responses only for operations that require authorization metadata.
/// This avoids brittle path-based heuristics.
/// </summary>
public sealed class AuthorizationResponsesOperationTransformer : IOpenApiOperationTransformer
{
    /// <summary>
    /// Inspects the endpoint metadata and appends 401/403 response entries when the operation
    /// requires authorization and does not allow anonymous access.
    /// </summary>
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        var endpointMetadata = context.Description.ActionDescriptor.EndpointMetadata;
        var hasAllowAnonymous = endpointMetadata.OfType<IAllowAnonymous>().Any();
        var hasAuthorize = endpointMetadata.OfType<IAuthorizeData>().Any();

        if (hasAuthorize && !hasAllowAnonymous)
        {
            OpenApiErrorResponseHelper.AddErrorResponse(
                operation,
                StatusCodes.Status401Unauthorized
            );
            OpenApiErrorResponseHelper.AddErrorResponse(operation, StatusCodes.Status403Forbidden);
        }

        return Task.CompletedTask;
    }
}
