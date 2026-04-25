using System.Net;
using System.Net.Http.Json;
using APITemplate.Tests.Integration.Helpers;
using Identity.Directory.Entities;
using Identity.Directory.Features.Role.CreateRole;
using Identity.Directory.Features.Role.Shared;
using Identity.Directory.Features.Role.UpdateRole;
using Identity.Directory.Features.User.AssignRoles;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Features;

[Trait("Category", "Integration")]
[Trait("Docker", "true")]
[Collection(IntegrationCollectionNames.HttpStateful)]
public sealed class RolesControllerTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private Tenant _adminTenant = default!;
    private AppUser _adminUser = default!;

    public RolesControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
    }

    public async ValueTask InitializeAsync()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (_adminTenant, _adminUser) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            "roles_admin",
            "roles_admin@test.com",
            ct: ct
        );
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private void AuthenticateAdmin(params string[] permissions) =>
        IntegrationAuthHelper.Authenticate(
            _client,
            userId: _adminUser.Id,
            tenantId: _adminTenant.Id,
            username: _adminUser.Username.Value,
            role: "TenantAdmin",
            permissions: permissions,
            email: _adminUser.Email.Value,
            subject: _adminUser.KeycloakUserId
        );

    [Fact]
    public async Task CreateRole_Success()
    {
        var ct = TestContext.Current.CancellationToken;
        AuthenticateAdmin("Roles.Create", "Roles.Read");

        var request = new CreateRoleRequest("Test Role", ["Test.Permission"]);
        var response = await _client.PostAsJsonAsync("/api/v1/roles", request, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var createdRole = await response.Content.ReadFromJsonAsync<RoleResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        createdRole.ShouldNotBeNull();
        createdRole!.Name.ShouldBe("Test Role");
        createdRole.Permissions.ShouldContain("Test.Permission");

        var listResponse = await _client.GetAsync("/api/v1/roles", ct);
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var roles = await listResponse.Content.ReadFromJsonAsync<List<RoleResponse>>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        roles.ShouldNotBeNull();
        roles!.ShouldContain(r => r.Id == createdRole.Id);
    }

    [Fact]
    public async Task UpdateRole_Success()
    {
        var ct = TestContext.Current.CancellationToken;
        AuthenticateAdmin("Roles.Create", "Roles.Update", "Roles.Read");

        var createReq = new CreateRoleRequest("To Update", []);
        var createRes = await _client.PostAsJsonAsync("/api/v1/roles", createReq, ct);
        var createdRole = await createRes.Content.ReadFromJsonAsync<RoleResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );

        var updateReq = new UpdateRoleRequest("Updated", ["New.Perm"]);
        var updateRes = await _client.PutAsJsonAsync(
            $"/api/v1/roles/{createdRole!.Id}",
            updateReq,
            ct
        );

        updateRes.StatusCode.ShouldBe(HttpStatusCode.OK);

        var listResponse = await _client.GetAsync("/api/v1/roles", ct);
        var roles = await listResponse.Content.ReadFromJsonAsync<List<RoleResponse>>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        roles!.First(r => r.Id == createdRole.Id).Name.ShouldBe("Updated");
        roles!.First(r => r.Id == createdRole.Id).Permissions.ShouldContain("New.Perm");
    }

    [Fact]
    public async Task DeleteRole_Success()
    {
        var ct = TestContext.Current.CancellationToken;
        AuthenticateAdmin("Roles.Create", "Roles.Delete", "Roles.Read");

        var createReq = new CreateRoleRequest("To Delete", []);
        var createRes = await _client.PostAsJsonAsync("/api/v1/roles", createReq, ct);
        var createdRole = await createRes.Content.ReadFromJsonAsync<RoleResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );

        var deleteRes = await _client.DeleteAsync($"/api/v1/roles/{createdRole!.Id}", ct);
        deleteRes.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var listResponse = await _client.GetAsync("/api/v1/roles", ct);
        var roles = await listResponse.Content.ReadFromJsonAsync<List<RoleResponse>>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        roles!.ShouldNotContain(r => r.Id == createdRole.Id);
    }

    [Fact]
    public async Task AssignUserRoles_Success()
    {
        var ct = TestContext.Current.CancellationToken;
        AppUser targetUser = await IntegrationAuthHelper.SeedUserInTenantAsync(
            _factory.Services,
            _adminTenant.Id,
            "assign_role_target",
            "assign_role_target@test.com",
            ct: ct
        );

        AuthenticateAdmin("Roles.Create", "Users.Update", "Roles.Read");

        var createReq = new CreateRoleRequest("Assigned Role", ["Some.Perm"]);
        var createRes = await _client.PostAsJsonAsync("/api/v1/roles", createReq, ct);
        var createdRole = await createRes.Content.ReadFromJsonAsync<RoleResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );

        var assignReq = new AssignUserRolesRequest([createdRole!.Id]);
        var assignRes = await _client.PostAsJsonAsync(
            $"/api/v1/users/{targetUser.Id}/roles",
            assignReq,
            ct
        );

        assignRes.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
