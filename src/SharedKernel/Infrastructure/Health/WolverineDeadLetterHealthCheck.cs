using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Wolverine.Logging;
using Wolverine.Persistence.Durability;

namespace SharedKernel.Infrastructure.Health;

public sealed class WolverineDeadLetterHealthCheck : IHealthCheck
{
    private readonly IMessageStore _messageStore;
    private readonly WolverineHealthCheckOptions _options;

    public WolverineDeadLetterHealthCheck(
        IMessageStore messageStore,
        IOptions<WolverineHealthCheckOptions> options
    )
    {
        _messageStore = messageStore;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            PersistedCounts counts = await _messageStore.Admin.FetchCountsAsync();

            Dictionary<string, object> data = new() { ["deadLetterCount"] = counts.DeadLetter };

            if (counts.DeadLetter >= _options.DeadLetterCriticalThreshold)
            {
                return HealthCheckResult.Unhealthy(
                    "Wolverine dead letter queue exceeds critical threshold",
                    data: data
                );
            }

            if (counts.DeadLetter >= _options.DeadLetterWarningThreshold)
            {
                return HealthCheckResult.Degraded(
                    "Wolverine dead letter queue exceeds warning threshold",
                    data: data
                );
            }

            return HealthCheckResult.Healthy(data: data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Failed to fetch Wolverine dead letter count", ex);
        }
    }
}
