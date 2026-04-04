using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace APITemplate.Api.OpenApi;

/// <summary>
///     Adds 401/403 responses only for operations that require authorization metadata.
/// </summary>
public sealed class AuthorizationResponsesOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        IList<object> endpointMetadata = context.Description.ActionDescriptor.EndpointMetadata;
        bool hasAllowAnonymous = endpointMetadata.OfType<IAllowAnonymous>().Any();
        bool hasAuthorize = endpointMetadata.OfType<IAuthorizeData>().Any();

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
