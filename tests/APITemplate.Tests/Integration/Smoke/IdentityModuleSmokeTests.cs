using System.Net;
using SharedKernel.Contracts.Security;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Smoke;

[Collection("Smoke")]
[Trait("Category", "Smoke")]
public sealed class IdentityModuleSmokeTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private HttpClient? _client;
    private Guid _seededUserId;
    private Guid _seededTenantId;

    private HttpClient Client => _client ??= _factory.CreateClient();

    public IdentityModuleSmokeTests(CustomWebApplicationFactory factory) => _factory = factory;

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var (tenant, user) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            username: "smokeuser",
            email: "smokeuser@example.com",
            ct: ct
        );
        _seededUserId = user.Id;
        _seededTenantId = tenant.Id;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task GetUsers_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(Client, permissions: [Permission.Users.Read]);
        var response = await Client.GetAsync("/api/v1/users", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetMe_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(
            Client,
            userId: _seededUserId,
            tenantId: _seededTenantId
        );
        var response = await Client.GetAsync("/api/v1/users/me", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTenants_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(Client, permissions: [Permission.Tenants.Read]);
        var response = await Client.GetAsync("/api/v1/tenants", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetRoles_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(Client, permissions: [Permission.Roles.Read]);
        var response = await Client.GetAsync("/api/v1/roles", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetRolePermissions_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(Client, permissions: [Permission.Roles.Read]);
        var response = await Client.GetAsync("/api/v1/roles/permissions", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTenantInvitations_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(Client, permissions: [Permission.Invitations.Read]);
        var response = await Client.GetAsync("/api/v1/tenant-invitations", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
