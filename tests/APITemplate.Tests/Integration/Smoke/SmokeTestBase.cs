using Identity.Auth.Security;
using Identity.Directory.Entities;
using Xunit;

namespace APITemplate.Tests.Integration.Smoke;

public abstract class SmokeTestBase : IAsyncLifetime
{
    protected sealed record SeededSmokeUser(
        Guid TenantId,
        Guid UserId,
        string Username,
        string Email,
        string KeycloakUserId
    );

    protected readonly CustomWebApplicationFactory Factory;
    protected Guid TenantId;

    protected HttpClient Client => field ??= Factory.CreateClient();

    protected SmokeTestBase(CustomWebApplicationFactory factory) => Factory = factory;

    protected abstract string UsernamePrefix { get; }

    private string ServiceAccountUsername =>
        $"{AuthConstants.Claims.ServiceAccountUsernamePrefix}{UsernamePrefix}";

    public virtual async ValueTask InitializeAsync()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await SeedUniqueTenantUserAsync(ct);
    }

    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;

    protected async Task<SeededSmokeUser> SeedUniqueTenantUserAsync(CancellationToken ct)
    {
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string username = $"{UsernamePrefix}-{suffix}";
        string email = $"{UsernamePrefix}-{suffix}@example.com";

        (Tenant Tenant, AppUser User) result = await IntegrationAuthHelper.SeedTenantUserAsync(
            Factory.Services,
            username: username,
            email: email,
            ct: ct
        );
        TenantId = result.Tenant.Id;
        return new SeededSmokeUser(
            result.Tenant.Id,
            result.User.Id,
            username,
            email,
            result.User.KeycloakUserId
                ?? throw new InvalidOperationException("Seeded smoke user must have a Keycloak id.")
        );
    }

    protected void AuthenticateAsServiceAccount(params string[] permissions) =>
        IntegrationAuthHelper.Authenticate(
            Client,
            tenantId: TenantId,
            username: ServiceAccountUsername,
            permissions: permissions
        );
}
