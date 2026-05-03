using BuildingBlocks.Web.Health;
using Notifications.Infrastructure.Health;
using ProductCatalog.Infrastructure.Health;

namespace APITemplate.Api;

/// <summary>
///     Add a new entry here when a module introduces its own <see cref="IHealthCheckModule" /> implementation.
/// </summary>
public static class HealthCheckModuleRegistry
{
    public static IReadOnlyList<Type> Modules { get; } =
    [
        typeof(SharedKernelHealthChecks),
        typeof(ProductCatalogHealthChecks),
        typeof(NotificationsHealthChecks),
    ];
}
