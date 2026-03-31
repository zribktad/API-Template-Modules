using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace ProductCatalog.Api.Controllers;

public static class ControllerExtensions
{
    public static string GetApiVersion(this ControllerBase controller) =>
        controller.RouteData.Values.TryGetValue("version", out object? version)
            ? version?.ToString() ?? "1"
            : "1";
}

