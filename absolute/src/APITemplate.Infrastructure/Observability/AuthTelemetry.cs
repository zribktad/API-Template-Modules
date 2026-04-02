using System.Diagnostics;
using System.Diagnostics.Metrics;
using APITemplate.Application.Common.Security;
using Microsoft.AspNetCore.Http;

namespace APITemplate.Infrastructure.Observability;

/// <summary>
/// Static facade for authentication-related telemetry, recording metric counters and
/// diagnostic activities for common auth failure scenarios in both JWT Bearer and BFF cookie flows.
/// </summary>
public static class AuthTelemetry
{
    private static readonly ActivitySource ActivitySource = new(
        ObservabilityConventions.ActivitySourceName
    );
    private static readonly Meter Meter = new(ObservabilityConventions.MeterName);

    private static readonly Counter<long> AuthFailures = Meter.CreateCounter<long>(
        TelemetryMetricNames.AuthFailures,
        description: "Authentication and BFF session failures grouped by scheme and reason."
    );

    /// <summary>Records a failure caused by a missing tenant claim in the validated token.</summary>
    public static void RecordMissingTenantClaim(HttpContext httpContext, string scheme) =>
        RecordFailure(
            TelemetryActivityNames.TokenValidated,
            scheme,
            TelemetryFailureReasons.MissingTenantClaim,
            ResolveSurface(httpContext.Request.Path)
        );

    /// <summary>Records a failure during the BFF cookie session refresh flow.</summary>
    public static void RecordCookieRefreshFailed(Exception? exception = null) =>
        RecordFailure(
            TelemetryActivityNames.CookieSessionRefresh,
            AuthConstants.BffSchemes.Cookie,
            TelemetryFailureReasons.RefreshFailed,
            TelemetrySurfaces.Bff,
            exception
        );

    /// <summary>Records a failure because no refresh token was present in the cookie properties.</summary>
    public static void RecordMissingRefreshToken() =>
        RecordFailure(
            TelemetryActivityNames.CookieSessionRefresh,
            AuthConstants.BffSchemes.Cookie,
            TelemetryFailureReasons.MissingRefreshToken,
            TelemetrySurfaces.Bff
        );

    /// <summary>Records a failure because the Keycloak token endpoint returned a non-success response.</summary>
    public static void RecordTokenEndpointRejected() =>
        RecordFailure(
            TelemetryActivityNames.CookieSessionRefresh,
            AuthConstants.BffSchemes.Cookie,
            TelemetryFailureReasons.TokenEndpointRejected,
            TelemetrySurfaces.Bff
        );

    /// <summary>Records a failure caused by an unhandled exception during token refresh.</summary>
    public static void RecordTokenRefreshException(Exception exception) =>
        RecordFailure(
            TelemetryActivityNames.CookieSessionRefresh,
            AuthConstants.BffSchemes.Cookie,
            TelemetryFailureReasons.TokenRefreshException,
            TelemetrySurfaces.Bff,
            exception
        );

    /// <summary>Records an unauthorized redirect-to-login event in the BFF cookie scheme.</summary>
    public static void RecordUnauthorizedRedirect() =>
        RecordFailure(
            TelemetryActivityNames.RedirectToLogin,
            AuthConstants.BffSchemes.Cookie,
            TelemetryFailureReasons.UnauthorizedRedirect,
            TelemetrySurfaces.Bff
        );

    private static void RecordFailure(
        string activityName,
        string scheme,
        string reason,
        string surface,
        Exception? exception = null
    )
    {
        AuthFailures.Add(
            1,
            [
                new KeyValuePair<string, object?>(TelemetryTagKeys.AuthScheme, scheme),
                new KeyValuePair<string, object?>(TelemetryTagKeys.AuthFailureReason, reason),
                new KeyValuePair<string, object?>(TelemetryTagKeys.ApiSurface, surface),
            ]
        );

        using var activity = ActivitySource.StartActivity(activityName, ActivityKind.Internal);
        activity?.SetTag(TelemetryTagKeys.AuthScheme, scheme);
        activity?.SetTag(TelemetryTagKeys.AuthFailureReason, reason);
        activity?.SetTag(TelemetryTagKeys.ApiSurface, surface);
        activity?.SetStatus(ActivityStatusCode.Error);
        if (exception is not null)
            activity?.SetTag(TelemetryTagKeys.ExceptionType, exception.GetType().Name);
    }

    private static string ResolveSurface(PathString path) =>
        TelemetryApiSurfaceResolver.Resolve(path);
}
