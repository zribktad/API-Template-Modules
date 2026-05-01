using APITemplate.Api.ExceptionHandling;
using SharedKernel.Application.Options.Http;
using SharedKernel.Contracts.Api;
using SharedKernel.Infrastructure.Configuration;

namespace APITemplate.Api.Extensions;

/// <summary>
///     Provides extension methods for configuring global error handling.
/// </summary>
public static class ErrorHandlingServiceCollectionExtensions
{
    /// <summary>
    ///     Registers RFC 7807 ProblemDetails, the global exception handler, and error-type metrics.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The updated IServiceCollection.</returns>
    public static IServiceCollection AddErrorHandling(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .AddValidatedOptions<ErrorDocumentationOptions>(
                configuration,
                validateDataAnnotations: false
            )
            .Validate(
                static o => ProblemDetailsErrorTypeUri.IsValidBaseUriWhenSet(o.ErrorTypeBaseUri),
                "ErrorDocumentation:ErrorTypeBaseUri must be an absolute http or https URI when set."
            );
        services.AddProblemDetails();
        services.ConfigureOptions<ProblemDetailsErrorTypeConfigureOptions>();
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddSingleton<ApiExceptionMetrics>();

        return services;
    }
}
