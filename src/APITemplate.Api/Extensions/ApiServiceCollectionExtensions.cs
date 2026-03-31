using APITemplate.Api.ExceptionHandling;
using APITemplate.Api.OpenApi;

namespace APITemplate.Api.Extensions;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddApiFoundation(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddProblemDetails(ApiProblemDetailsOptions.Configure);
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddHealthChecks();
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<HealthCheckOpenApiDocumentTransformer>();
            options.AddDocumentTransformer<ProblemDetailsOpenApiTransformer>();
        });

        return services;
    }
}
