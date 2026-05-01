using APITemplate.Api.Middleware;

namespace APITemplate.Api.Extensions;

/// <summary>
///     Provides extension methods for registering infrastructure diagnostics.
/// </summary>
public static class DiagnosticsServiceCollectionExtensions
{
    /// <summary>
    ///     Registers startup filters for infrastructure diagnostics.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <returns>The updated IServiceCollection.</returns>
    public static IServiceCollection AddInfrastructureDiagnostics(this IServiceCollection services)
    {
        services.AddSingleton<IStartupFilter, InfrastructureDiagnosticStartupFilter>();
        return services;
    }
}
