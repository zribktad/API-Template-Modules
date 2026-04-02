using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace APITemplate.Infrastructure.Observability;

/// <summary>
/// Resolves the normalized route template for the current HTTP request, substituting
/// the <c>{version}</c> route token with its actual value to produce stable metric tags.
/// </summary>
public static partial class HttpRouteResolver
{
    [GeneratedRegex(
        @"\{version(?::[^}]*)?\}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex VersionTokenRegex();

    /// <summary>
    /// Returns the route template for the matched endpoint, falling back to the raw request path
    /// when no route endpoint is matched. The version token is replaced with its actual value.
    /// </summary>
    public static string Resolve(HttpContext httpContext)
    {
        var routeTemplate = httpContext.GetEndpoint() is RouteEndpoint routeEndpoint
            ? routeEndpoint.RoutePattern.RawText
            : null;

        if (string.IsNullOrWhiteSpace(routeTemplate))
            return httpContext.Request.Path.Value ?? TelemetryDefaults.Unknown;

        return ReplaceVersionToken(routeTemplate, httpContext.Request.RouteValues);
    }

    /// <summary>
    /// Replaces the first <c>{version:...}</c> token in <paramref name="routeTemplate"/> with
    /// the actual version value from <paramref name="routeValues"/>, or returns the template unchanged
    /// when no version route value is present.
    /// </summary>
    public static string ReplaceVersionToken(string routeTemplate, RouteValueDictionary routeValues)
    {
        if (string.IsNullOrWhiteSpace(routeTemplate))
            return TelemetryDefaults.Unknown;

        if (!routeValues.TryGetValue("version", out var versionValue) || versionValue is null)
            return routeTemplate;

        var version = versionValue.ToString();
        if (string.IsNullOrWhiteSpace(version))
            return routeTemplate;

        return VersionTokenRegex().Replace(routeTemplate, version, 1);
    }
}
