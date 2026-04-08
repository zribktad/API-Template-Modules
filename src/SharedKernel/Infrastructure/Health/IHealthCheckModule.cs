using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Infrastructure.Health;

public interface IHealthCheckModule
{
    void RegisterHealthChecks(IHealthChecksBuilder builder);
}
