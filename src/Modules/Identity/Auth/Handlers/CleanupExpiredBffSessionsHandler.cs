using Identity.Logging;
using Identity.Auth.Options;
using Identity.Auth.Security.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel.Contracts.Commands.Cleanup;

namespace Identity.Auth.Handlers;

/// <summary>
///     Wolverine handler that processes <see cref="CleanupExpiredBffSessionsCommand" /> dispatched by the
///     BackgroundJobs module. Permanently deletes BFF sessions whose refresh tokens have expired,
///     that have been in a terminal state (Revoked/Expired) past the configurable audit retention window,
///     or that lack a known refresh expiry and exceeded the absolute session lifetime (safety net).
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
        DateTimeOffset now = timeProvider.GetUtcNow();

        // Terminal sessions (Revoked/Expired): configurable retention for audit trail
        DateTimeOffset terminalCutoff = now.AddHours(-options.TerminalSessionRetentionHours);
        // Safety net: sessions without known refresh expiry that exceeded absolute lifetime
        DateTimeOffset absoluteCutoff = now.AddMinutes(-options.SessionAbsoluteLifetimeMinutes);

        int totalDeleted = 0;
        int deleted;

        do
        {
            List<Guid> ids = await dbContext
                .BffSessions.IgnoreQueryFilters()
                .Where(s =>
                    // Refresh token expired — session is permanently dead, no recovery possible
                    (s.RefreshTokenExpiresAtUtc != null && s.RefreshTokenExpiresAtUtc < now)
                    // Terminal sessions past configurable audit retention.
                    // Defense in depth: even if RefreshTokenExpiresAtUtc is wrong or missing,
                    // sessions explicitly marked as terminal will still be cleaned up.
                    // Revoked = explicitly invalidated (logout, refresh rejected, token replay, etc.)
                    || (s.Status == BffSessionStatus.Revoked && s.RevokedAtUtc < terminalCutoff)
                    // Expired = refresh token expiry detected at read time by BffSessionService
                    || (s.Status == BffSessionStatus.Expired && s.LastSeenAtUtc < terminalCutoff)
                    // Safety net: no known refresh expiry (pre-first-refresh), past absolute lifetime
                    || (s.RefreshTokenExpiresAtUtc == null && s.CreatedAtUtc < absoluteCutoff)
                )
                .OrderBy(s => s.CreatedAtUtc)
                .Take(command.BatchSize)
                .Select(s => s.Id)
                .ToListAsync(ct);

            if (ids.Count == 0)
                break;

            deleted = await dbContext
                .BffSessions.IgnoreQueryFilters()
                .Where(s => ids.Contains(s.Id))
                .ExecuteDeleteAsync(ct);

            totalDeleted += deleted;
        } while (deleted == command.BatchSize);

        if (totalDeleted > 0)
            logger.ExpiredBffSessionsCleanedUp(totalDeleted);
    }
}
