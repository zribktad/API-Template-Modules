using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace APITemplate.Api.Extensions;

/// <summary>
/// Presentation-layer helper extensions for <see cref="ControllerBase"/> providing
/// convenient access to API versioning metadata.
/// </summary>
public static class ControllerExtensions
{
    /// <summary>
    /// Returns the API version string (e.g. <c>"1"</c>) from the current request context,
    /// used when building Location headers for Created/Accepted responses.
    /// </summary>
    public static string GetApiVersion(this ControllerBase controller) =>
        controller.HttpContext.GetRequestedApiVersion()!.ToString();
}
