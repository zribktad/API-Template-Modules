using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BuildingBlocks.Web.Health;

public static class HealthCheckEndpointConfiguration
{
    public static IReadOnlyList<HealthCheckEndpointDefinition> Endpoints { get; } =
    [
        new(
            "/health/live",
            "Liveness probe",
            "Returns whether the application process is alive.",
            reg => reg.Tags.Contains(HealthCheckTags.Live)
        ),
        new(
            "/health/ready",
            "Readiness probe",
            "Returns whether the application can serve traffic.",
            reg => reg.Tags.Contains(HealthCheckTags.Ready)
        ),
        new(
            "/health",
            "Comprehensive health check",
            "Returns the health status of all registered services.",
            Predicate: null
        ),
    ];
}

public sealed record HealthCheckEndpointDefinition(
    string Path,
    string Summary,
    string Description,
    Func<HealthCheckRegistration, bool>? Predicate
);

