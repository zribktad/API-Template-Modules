using Asp.Versioning;

namespace APITemplate.Api.Extensions;

/// <summary>
///     Provides extension methods for configuring API versioning.
/// </summary>
public static class ApiVersioningServiceCollectionExtensions
{
    /// <summary>
    ///     Registers API versioning and API explorer services with default settings.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <returns>The updated IServiceCollection.</returns>
    public static IServiceCollection AddApiVersioningRegistration(this IServiceCollection services)
    {
        services
            .AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

        return services;
    }
}
