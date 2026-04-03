using Contracts.Commands.Cleanup;
using Identity.Infrastructure.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Handlers;

/// <summary>
/// Wolverine handler that processes <see cref="CleanupExpiredInvitationsCommand"/> dispatched by the
/// BackgroundJobs module. Permanently deletes pending tenant invitations whose expiry is older than
/// the configured retention window, using batched bulk deletes for efficiency.
/// </summary>
public sealed class CleanupExpiredInvitationsHandler
{
    public static async Task HandleAsync(
        CleanupExpiredInvitationsCommand command,
        IdentityDbContext dbContext,
        TimeProvider timeProvider,
        ILogger<CleanupExpiredInvitationsHandler> logger,
        CancellationToken ct
    )
    {
        DateTime cutoff = timeProvider.GetUtcNow().UtcDateTime.AddHours(-command.RetentionHours);
        int totalDeleted = 0;
        int deleted;

        do
        {
            deleted = await dbContext
                .TenantInvitations.IgnoreQueryFilters()
                .Where(i => i.Status == InvitationStatus.Pending && i.ExpiresAtUtc < cutoff)
                .OrderBy(i => i.ExpiresAtUtc)
                .Take(command.BatchSize)
                .ExecuteDeleteAsync(ct);

            totalDeleted += deleted;
        } while (deleted == command.BatchSize);

        if (totalDeleted > 0)
        {
            logger.ExpiredInvitationsCleanedUp(totalDeleted);
        }
    }
}
