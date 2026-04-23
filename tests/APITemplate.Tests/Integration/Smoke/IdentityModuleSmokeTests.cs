using System.Net;
using SharedKernel.Contracts.Security;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Smoke;

[Collection("Smoke")]
[Trait("Category", "Smoke")]
public sealed class IdentityModuleSmokeTests : SmokeTestBase
{
    private Guid _seededUserId;
    private string _seededEmail = string.Empty;
    private string _seededKeycloakUserId = string.Empty;
    private string _seededUsername = string.Empty;

    public IdentityModuleSmokeTests(CustomWebApplicationFactory factory)
        : base(factory) { }

    protected override string UsernamePrefix => "smokeuser";

    public override async ValueTask InitializeAsync()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        SeededSmokeUser seededUser = await SeedUniqueTenantUserAsync(ct);
        _seededUserId = seededUser.UserId;
        _seededEmail = seededUser.Email;
        _seededKeycloakUserId = seededUser.KeycloakUserId;
        _seededUsername = seededUser.Username;
    }

    [Fact]
    public async Task GetUsers_ReturnsOk()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AuthenticateAsServiceAccount(Permission.Users.Read);
        HttpResponseMessage response = await Client.GetAsync("/api/v1/users", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetMe_ReturnsOk()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(
            Client,
            userId: _seededUserId,
            tenantId: TenantId,
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
        AuthenticateAsServiceAccount(Permission.Tenants.Read);
        HttpResponseMessage response = await Client.GetAsync("/api/v1/tenants", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetRoles_ReturnsOk()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AuthenticateAsServiceAccount(Permission.Roles.Read);
        HttpResponseMessage response = await Client.GetAsync("/api/v1/roles", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetRolePermissions_ReturnsOk()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AuthenticateAsServiceAccount(Permission.Roles.Read);
        HttpResponseMessage response = await Client.GetAsync("/api/v1/roles/permissions", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTenantInvitations_ReturnsOk()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AuthenticateAsServiceAccount(Permission.Invitations.Read);
        HttpResponseMessage response = await Client.GetAsync("/api/v1/tenant-invitations", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
