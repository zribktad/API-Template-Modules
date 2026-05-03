using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Web.Health;

public interface IHealthCheckModule
{
    void RegisterHealthChecks(IHealthChecksBuilder builder);
}

