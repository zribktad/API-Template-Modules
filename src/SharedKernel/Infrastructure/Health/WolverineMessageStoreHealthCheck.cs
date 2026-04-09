using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Wolverine.Logging;
using Wolverine.Persistence.Durability;

namespace SharedKernel.Infrastructure.Health;

public sealed class WolverineMessageStoreHealthCheck : IHealthCheck
{
    private readonly IMessageStore _messageStore;
    private readonly WolverineHealthCheckOptions _options;

    public WolverineMessageStoreHealthCheck(
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
            await _messageStore.Admin.CheckConnectivityAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Wolverine message store is not reachable", ex);
        }

        try
        {
            PersistedCounts counts = await _messageStore.Admin.FetchCountsAsync();

            Dictionary<string, object> data = new()
            {
                ["incoming"] = counts.Incoming,
                ["outgoing"] = counts.Outgoing,
                ["scheduled"] = counts.Scheduled,
                ["handled"] = counts.Handled,
            };

            bool outgoingBacklog = counts.Outgoing >= _options.OutgoingBacklogWarningThreshold;
            bool incomingBacklog = counts.Incoming >= _options.IncomingBacklogWarningThreshold;

            if (outgoingBacklog || incomingBacklog)
            {
                return HealthCheckResult.Degraded(
                    "Wolverine message backlog exceeds warning threshold",
                    data: data
                );
            }

            return HealthCheckResult.Healthy(data: data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Failed to fetch Wolverine message counts", ex);
        }
    }
}
