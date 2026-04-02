using Microsoft.AspNetCore.Http;

namespace APITemplate.Infrastructure.Observability;

/// <summary>
/// Maps an HTTP request path to a logical API surface name (e.g., graphql, health, rest)
/// for use as a telemetry tag value.
/// </summary>
public static class TelemetryApiSurfaceResolver
{
    /// <summary>
    /// Returns the surface name for the given request path by matching well-known prefixes;
    /// falls back to <see cref="TelemetrySurfaces.Rest"/> for all other paths.
    /// </summary>
    public static string Resolve(PathString path)
    {
        if (path.StartsWithSegments(TelemetryPathPrefixes.GraphQl))
            return TelemetrySurfaces.GraphQl;

        if (path.StartsWithSegments(TelemetryPathPrefixes.Health))
            return TelemetrySurfaces.Health;

        if (
            path.StartsWithSegments(TelemetryPathPrefixes.Scalar)
            || path.StartsWithSegments(TelemetryPathPrefixes.OpenApi)
        )
        {
            return TelemetrySurfaces.Documentation;
        }

        return TelemetrySurfaces.Rest;
    }
}
