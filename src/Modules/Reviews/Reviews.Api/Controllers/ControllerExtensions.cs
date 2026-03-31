using Microsoft.AspNetCore.Mvc;

namespace Reviews.Api.Controllers;

public static class ControllerExtensions
{
    public static string GetApiVersion(this ControllerBase controller) =>
        controller.RouteData.Values.TryGetValue("version", out object? version)
            ? version?.ToString() ?? "1"
            : "1";
}
