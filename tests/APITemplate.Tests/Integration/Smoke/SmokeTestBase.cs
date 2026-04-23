using Identity.Auth.Security;
using Identity.Directory.Entities;
using Xunit;

namespace APITemplate.Tests.Integration.Smoke;

public abstract class SmokeTestBase : IAsyncLifetime
{
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

    protected async Task<(Tenant Tenant, AppUser User)> SeedUniqueTenantUserAsync(
        CancellationToken ct
    )
    {
        string suffix = Guid.NewGuid().ToString("N")[..8];
        (Tenant Tenant, AppUser User) result = await IntegrationAuthHelper.SeedTenantUserAsync(
            Factory.Services,
            username: $"{UsernamePrefix}-{suffix}",
            email: $"{UsernamePrefix}-{suffix}@example.com",
            ct: ct
        );
        TenantId = result.Tenant.Id;
        return result;
    }

    protected void AuthenticateAsServiceAccount(params string[] permissions) =>
        IntegrationAuthHelper.Authenticate(
            Client,
            tenantId: TenantId,
            username: ServiceAccountUsername,
            permissions: permissions
        );
}
