using ProductCatalog.Infrastructure.Health;
using SharedKernel.Infrastructure.Health;

namespace APITemplate.Api;

/// <summary>
///     Add a new entry here when a module introduces its own <see cref="IHealthCheckModule" /> implementation.
/// </summary>
public static class HealthCheckModuleRegistry
{
    public static IReadOnlyList<Type> Modules { get; } =
    [typeof(SharedKernelHealthChecks), typeof(ProductCatalogHealthChecks)];
}
