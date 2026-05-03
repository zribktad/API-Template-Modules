using Microsoft.AspNetCore.Mvc;

namespace BuildingBlocks.Web.Api;

public static class ControllerExtensions
{
    public static string GetApiVersion(this ControllerBase controller)
    {
        return controller.RouteData.Values.TryGetValue("version", out object? version)
            ? version?.ToString() ?? "1"
            : "1";
    }
}

