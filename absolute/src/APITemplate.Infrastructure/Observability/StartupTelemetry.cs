using System.Diagnostics;

namespace APITemplate.Infrastructure.Observability;

/// <summary>
/// Static facade for startup-phase telemetry, creating diagnostic activities for each
/// startup task (migration, seeding, readiness checks) so they appear as spans in traces.
/// </summary>
public static class StartupTelemetry
{
    private static readonly ActivitySource ActivitySource = new(
        ObservabilityConventions.ActivitySourceName
    );

    /// <summary>Starts a traced startup scope for the relational (PostgreSQL) migration step.</summary>
    public static Scope StartRelationalMigration() =>
        StartStep(
            TelemetryStartupSteps.Migrate,
            TelemetryStartupComponents.PostgreSql,
            TelemetryDatabaseSystems.PostgreSql
        );

    /// <summary>Starts a traced startup scope for the MongoDB migration step.</summary>
    public static Scope StartMongoMigration() =>
        StartStep(
            TelemetryStartupSteps.Migrate,
            TelemetryStartupComponents.MongoDb,
            TelemetryDatabaseSystems.MongoDb
        );

    /// <summary>Starts a traced startup scope for the auth-bootstrap seeding step.</summary>
    public static Scope StartAuthBootstrapSeed() =>
        StartStep(
            TelemetryStartupSteps.SeedAuthBootstrap,
            TelemetryStartupComponents.AuthBootstrap
        );

    /// <summary>Starts a traced startup scope for the Keycloak readiness-check step.</summary>
    public static Scope StartKeycloakReadinessCheck() =>
        StartStep(TelemetryStartupSteps.WaitKeycloakReady, TelemetryStartupComponents.Keycloak);

    private static Scope StartStep(string step, string component, string? dbSystem = null)
    {
        var activity = StartActivity(step, component);
        if (!string.IsNullOrWhiteSpace(dbSystem))
            activity?.SetTag(TelemetryTagKeys.DbSystem, dbSystem);

        return new Scope(activity);
    }

    private static Activity? StartActivity(string step, string component)
    {
        var activity = ActivitySource.StartActivity(
            TelemetryActivityNames.Startup(step),
            ActivityKind.Internal
        );
        activity?.SetTag(TelemetryTagKeys.StartupStep, step);
        activity?.SetTag(TelemetryTagKeys.StartupComponent, component);
        return activity;
    }

    /// <summary>
    /// Represents an active startup telemetry scope. Call <see cref="Fail"/> to mark the
    /// underlying activity as failed, then dispose to end the activity.
    /// </summary>
    public sealed class Scope(Activity? activity) : IDisposable
    {
        private readonly Activity? _activity = activity;

        /// <summary>Marks the underlying activity as failed and records the exception type.</summary>
        public void Fail(Exception exception)
        {
            if (_activity is not null)
            {
                _activity.SetStatus(ActivityStatusCode.Error, exception.Message);
                _activity.SetTag(TelemetryTagKeys.StartupSuccess, false);
                _activity.SetTag(TelemetryTagKeys.ExceptionType, exception.GetType().Name);
            }
        }

        public void Dispose() => _activity?.Dispose();
    }
}
