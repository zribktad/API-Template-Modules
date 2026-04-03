using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using ProductCatalog.Logging;

namespace ProductCatalog.Persistence;

public sealed class MongoDbHealthCheck : IHealthCheck
{
    private readonly MongoDbContext _mongoDbContext;
    private readonly ILogger<MongoDbHealthCheck> _logger;

    public MongoDbHealthCheck(MongoDbContext mongoDbContext, ILogger<MongoDbHealthCheck> logger)
    {
        _mongoDbContext = mongoDbContext;
        _logger = logger;
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
            _logger.MongoDbHealthCheckFailed(ex);
            return HealthCheckResult.Unhealthy("MongoDB is not reachable", ex);
        }
    }
}

