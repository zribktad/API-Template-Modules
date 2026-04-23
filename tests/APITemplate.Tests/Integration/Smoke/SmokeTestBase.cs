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

    protected readonly CustomWebApplicationFactory _factory;
    protected SeededSmokeUser SeededUser { get; private set; } = default!;

    protected HttpClient Client => field ??= _factory.CreateClient();

    protected SmokeTestBase(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    protected abstract string UsernamePrefix { get; }

    public virtual async ValueTask InitializeAsync()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        SeededUser = await SeedUniqueTenantUserAsync(ct);
    }

    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;

    protected async Task<SeededSmokeUser> SeedUniqueTenantUserAsync(CancellationToken ct)
    {
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string username = $"{UsernamePrefix}-{suffix}";
        string email = $"{UsernamePrefix}-{suffix}@example.com";

        (Tenant Tenant, AppUser User) result = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            username: username,
            email: email,
            ct: ct
        );
        return new SeededSmokeUser(
            result.Tenant.Id,
            result.User.Id,
            username,
            email,
            result.User.KeycloakUserId
                ?? throw new InvalidOperationException("Seeded smoke user must have a Keycloak id.")
        );
    }

    protected void AuthenticateAsSeededUser(
        string[]? permissions = null,
        string role = "User",
        string? subject = null
    ) =>
        IntegrationAuthHelper.Authenticate(
            Client,
            userId: SeededUser.UserId,
            tenantId: SeededUser.TenantId,
            username: SeededUser.Username,
            role: role,
            permissions: permissions,
            email: SeededUser.Email,
            // Smoke users are pre-linked by Keycloak subject; keep token sub aligned by default.
            subject: subject ?? SeededUser.KeycloakUserId
        );
}
