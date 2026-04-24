using System.Net;
using SharedKernel.Contracts.Security;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Smoke;

[Collection("Smoke")]
[Trait("Category", "Smoke")]
[Trait("Docker", "true")]
public sealed class IdentityModuleSmokeTests : SmokeTestBase
{
    public IdentityModuleSmokeTests(CustomWebApplicationFactory factory)
        : base(factory) { }

    protected override string UsernamePrefix => "smokeuser";

    [Fact]
    public async Task GetUsers_ReturnsOk()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AuthenticateAsSeededUser([Permission.Users.Read]);
        HttpResponseMessage response = await Client.GetAsync("/api/v1/users", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetMe_ReturnsOk()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AuthenticateAsSeededUser();
        HttpResponseMessage response = await Client.GetAsync("/api/v1/users/me", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTenants_ReturnsOk()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AuthenticateAsSeededUser([Permission.Tenants.Read]);
        HttpResponseMessage response = await Client.GetAsync("/api/v1/tenants", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetRoles_ReturnsOk()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AuthenticateAsSeededUser([Permission.Roles.Read]);
        HttpResponseMessage response = await Client.GetAsync("/api/v1/roles", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetRolePermissions_ReturnsOk()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AuthenticateAsSeededUser([Permission.Roles.Read]);
        HttpResponseMessage response = await Client.GetAsync("/api/v1/roles/permissions", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTenantInvitations_ReturnsOk()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AuthenticateAsSeededUser([Permission.Invitations.Read]);
        HttpResponseMessage response = await Client.GetAsync("/api/v1/tenant-invitations", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
