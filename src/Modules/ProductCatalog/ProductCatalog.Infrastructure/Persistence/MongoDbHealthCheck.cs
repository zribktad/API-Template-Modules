using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ProductCatalog.Infrastructure.Persistence;

public sealed class MongoDbHealthCheck : IHealthCheck
{
    private readonly MongoDbContext _mongoDbContext;

    public MongoDbHealthCheck(MongoDbContext mongoDbContext)
    {
        _mongoDbContext = mongoDbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await _mongoDbContext.PingAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MongoDB is not reachable", ex);
        }
    }
}
