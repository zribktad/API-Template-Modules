using System.Globalization;
using BuildingBlocks.Application.Context;
using Identity.Auth.Security.Sessions;

namespace APITemplate.Tests.Unit.Identity;

/// <summary>
///     Shared fixtures for <see cref="PostgresCachedBffSessionStore" /> and
///     <see cref="PostgresDistributedCacheBffSessionStore" /> unit tests.
/// </summary>
internal static class BffSessionStoreUnitTestHelpers
{
    internal static readonly DateTimeOffset DefaultSessionEpoch = DateTimeOffset.Parse(
        "2026-04-10T12:00:00Z",
        CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal
    );

    internal static BffSessionRecord CreateSampleSession(string? sessionId = null)
    {
        DateTimeOffset t = DefaultSessionEpoch;
        return new BffSessionRecord
        {
            SessionId = sessionId ?? BffSessionIds.NewId(),
            UserId = Guid.NewGuid().ToString(),
            Subject = Guid.NewGuid().ToString(),
            Provider = BffProviderType.Keycloak,
            Email = "test@example.com",
            DisplayName = "Test User",
            AccessToken = "access-token-value",
            RefreshToken = "refresh-token-value",
            AccessTokenExpiresAtUtc = t.AddMinutes(5),
            CreatedAtUtc = t,
            LastSeenAtUtc = t,
            LastRefreshedAtUtc = t,
            Status = BffSessionStatus.Active,
            Version = 0,
        };
    }

    internal static BffSessionRecord CreateTerminalSession(
        string? id = null,
        BffSessionStatus status = BffSessionStatus.Revoked
    ) =>
        CreateSampleSession(id) with
        {
            Status = status,
            RevokedAtUtc = status == BffSessionStatus.Revoked ? DefaultSessionEpoch : null,
            RevocationReason =
                status == BffSessionStatus.Revoked ? BffSessionRevocationReason.Logout : null,
        };

    internal static BffSessionRecord CreateExpiredRefreshSession(string? id = null) =>
        CreateSampleSession(id) with
        {
            RefreshTokenExpiresAtUtc = DefaultSessionEpoch.AddMinutes(-1),
        };

    internal static BffSessionRecord CreateAbsoluteLifetimeExceededSession(TimeSpan lifetime) =>
        CreateSampleSession() with
        {
            CreatedAtUtc = DefaultSessionEpoch - lifetime - TimeSpan.FromMinutes(1),
        };

    internal static BffSessionRecord CreateSessionWithVersion(long version, string? id = null) =>
        CreateSampleSession(id) with
        {
            Version = version,
        };

    internal static async Task<bool> WaitUntilAsync(
        Func<bool> predicate,
        CancellationToken ct,
        TimeSpan? delay = null
    )
    {
        TimeSpan pollingDelay = delay ?? TimeSpan.FromMilliseconds(25);
        for (int attempt = 0; attempt < 20; attempt++)
        {
            if (predicate())
                return true;

            await Task.Delay(pollingDelay, ct);
        }

        return predicate();
    }

    internal sealed class StubTenantProvider : ITenantProvider
    {
        public Guid TenantId => Guid.Empty;
        public bool HasTenant => false;
    }

    internal sealed class StubActorProvider : IActorProvider
    {
        public Guid ActorId => Guid.Empty;
    }
}
