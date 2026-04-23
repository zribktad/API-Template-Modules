using System.Net;
using Identity.Auth.Security;
using SharedKernel.Contracts.Security;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Smoke;

[Collection("Smoke")]
[Trait("Category", "Smoke")]
public sealed class IdentityModuleSmokeTests : IAsyncLifetime
{
    private const string ServiceAccountUsername =
        $"{AuthConstants.Claims.ServiceAccountUsernamePrefix}smoke-identity";
    private readonly CustomWebApplicationFactory _factory;
    private string _seededEmail = string.Empty;
    private string _seededKeycloakUserId = string.Empty;
    private Guid _seededTenantId;
    private string _seededUsername = string.Empty;

    private HttpClient Client => field ??= _factory.CreateClient();

    public IdentityModuleSmokeTests(CustomWebApplicationFactory factory) => _factory = factory;

    public async ValueTask InitializeAsync()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string suffix = Guid.NewGuid().ToString("N")[..8];
        (Identity.Directory.Entities.Tenant? tenant, Identity.Directory.Entities.AppUser? user) =
            await IntegrationAuthHelper.SeedTenantUserAsync(
                _factory.Services,
                username: $"smokeuser-{suffix}",
                email: $"smokeuser-{suffix}@example.com",
                ct: ct
            );
        _seededEmail = user.Email.Value;
        _seededKeycloakUserId = user.KeycloakUserId.ShouldNotBeNull();
        _seededTenantId = tenant.Id;
        _seededUsername = user.Username;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task GetUsers_ReturnsOk()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(
            Client,
            tenantId: _seededTenantId,
            username: ServiceAccountUsername,
            permissions: [Permission.Users.Read]
        );
        HttpResponseMessage response = await Client.GetAsync("/api/v1/users", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetMe_ReturnsOk()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(
            Client,
            tenantId: _seededTenantId,
            username: _seededUsername,
            subject: _seededKeycloakUserId,
            role: "User",
            email: _seededEmail
        );
        HttpResponseMessage response = await Client.GetAsync("/api/v1/users/me", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTenants_ReturnsOk()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(
            Client,
            tenantId: _seededTenantId,
            username: ServiceAccountUsername,
            permissions: [Permission.Tenants.Read]
        );
        HttpResponseMessage response = await Client.GetAsync("/api/v1/tenants", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetRoles_ReturnsOk()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(
            Client,
            tenantId: _seededTenantId,
            username: ServiceAccountUsername,
            permissions: [Permission.Roles.Read]
        );
        HttpResponseMessage response = await Client.GetAsync("/api/v1/roles", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetRolePermissions_ReturnsOk()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(
            Client,
            tenantId: _seededTenantId,
            username: ServiceAccountUsername,
            permissions: [Permission.Roles.Read]
        );
        HttpResponseMessage response = await Client.GetAsync("/api/v1/roles/permissions", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTenantInvitations_ReturnsOk()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(
            Client,
            tenantId: _seededTenantId,
            username: ServiceAccountUsername,
            permissions: [Permission.Invitations.Read]
        );
        HttpResponseMessage response = await Client.GetAsync("/api/v1/tenant-invitations", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
