using APITemplate.Api.OpenApi;

namespace APITemplate.Api.Extensions;

/// <summary>
///     Provides extension methods for configuring OpenAPI documentation.
/// </summary>
public static class OpenApiServiceCollectionExtensions
{
    /// <summary>
    ///     Registers OpenAPI services and applies custom document and operation transformers.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <returns>The updated IServiceCollection.</returns>
    public static IServiceCollection AddOpenApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<HealthCheckOpenApiDocumentTransformer>();
            options.AddDocumentTransformer<BearerSecuritySchemeDocumentTransformer>();
            options.AddDocumentTransformer<ProblemDetailsOpenApiTransformer>();
            options.AddOperationTransformer<AuthorizationResponsesOperationTransformer>();
        });

        return services;
    }
}
