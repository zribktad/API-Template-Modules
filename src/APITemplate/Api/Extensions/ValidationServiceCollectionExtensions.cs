using BuildingBlocks.Application.Batch;
using BuildingBlocks.Application.Batch.Rules;
using BuildingBlocks.Application.Validation;

namespace APITemplate.Api.Extensions;

/// <summary>
///     Provides extension methods for configuring request validation.
/// </summary>
public static class ValidationServiceCollectionExtensions
{
    /// <summary>
    ///     Registers data-annotation-based validators and the ASP.NET Core validation pipeline.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <returns>The updated IServiceCollection.</returns>
    public static IServiceCollection AddRequestValidation(this IServiceCollection services)
    {
        services.AddSingleton<IValidator, DataAnnotationsValidator>();
        services.AddScoped(typeof(IBatchRule<>), typeof(DataAnnotationsBatchRule<>));
        Microsoft.Extensions.DependencyInjection.ValidationServiceCollectionExtensions.AddValidation(
            services
        );

        return services;
    }
}
