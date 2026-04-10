using Identity.Logging;
using Identity.Options;
using Identity.Security.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel.Contracts.Commands.Cleanup;

namespace Identity.Handlers;

/// <summary>
///     Wolverine handler that processes <see cref="CleanupExpiredBffSessionsCommand" /> dispatched by the
///     BackgroundJobs module. Permanently deletes BFF sessions that are revoked (past 24h retention),
///     idle-expired, or have exceeded the absolute lifetime, using batched bulk deletes.
/// </summary>
public sealed class CleanupExpiredBffSessionsHandler
{
    public static async Task HandleAsync(
        CleanupExpiredBffSessionsCommand command,
        IdentityDbContext dbContext,
        IOptions<BffOptions> bffOptions,
        TimeProvider timeProvider,
        ILogger<CleanupExpiredBffSessionsHandler> logger,
        CancellationToken ct
    )
    {
        BffOptions options = bffOptions.Value;
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;

        // Revoked sessions: 24h retention for audit trail
        DateTime revokedCutoff = now.AddHours(-24);
        // Idle expired: no activity within SessionIdleTimeoutMinutes
        DateTime idleCutoff = now.AddMinutes(-options.SessionIdleTimeoutMinutes);
        // Absolute expired: session exceeded SessionAbsoluteLifetimeMinutes
        DateTime absoluteCutoff = now.AddMinutes(-options.SessionAbsoluteLifetimeMinutes);

        int totalDeleted = 0;
        int deleted;

        do
        {
            deleted = await dbContext
                .BffSessions.IgnoreQueryFilters()
                .Where(s =>
                    (s.Status == BffSessionStatus.Revoked && s.RevokedAtUtc < revokedCutoff)
                    || s.LastSeenAtUtc < idleCutoff
                    || s.CreatedAtUtc < absoluteCutoff
                )
                .OrderBy(s => s.LastSeenAtUtc)
                .Take(command.BatchSize)
                .ExecuteDeleteAsync(ct);

            totalDeleted += deleted;
        } while (deleted == command.BatchSize);

        if (totalDeleted > 0)
            logger.ExpiredBffSessionsCleanedUp(totalDeleted);
    }
}
