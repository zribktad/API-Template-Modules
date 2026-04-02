using APITemplate.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace APITemplate.Infrastructure.Health;

/// <summary>
/// ASP.NET Core health check that verifies MongoDB availability by sending a ping command
/// to the configured database with a 5-second timeout.
/// </summary>
public sealed class MongoDbHealthCheck : IHealthCheck
{
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(5);

    private readonly MongoDbContext _context;

    public MongoDbHealthCheck(MongoDbContext context) => _context = context;

    /// <summary>
    /// Pings MongoDB within the timeout and returns <see cref="HealthCheckResult.Healthy"/>
    /// on success or <see cref="HealthCheckResult.Unhealthy"/> if the ping fails.
    /// </summary>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(CheckTimeout);
            await _context.PingAsync(cts.Token);
            return HealthCheckResult.Healthy();
        }
        catch
        {
            return HealthCheckResult.Unhealthy("MongoDB is not reachable");
        }
    }
}
