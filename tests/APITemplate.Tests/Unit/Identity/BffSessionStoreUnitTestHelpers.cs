using System.Globalization;
using Identity.Auth.Security.Sessions;
using SharedKernel.Application.Context;

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
            SessionId = sessionId ?? Guid.NewGuid().ToString("N"),
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
