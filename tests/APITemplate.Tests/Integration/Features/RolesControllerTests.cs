using System.Net;
using System.Net.Http.Json;
using APITemplate.Tests.Integration.Helpers;
using Identity.Directory.Features.Role.CreateRole;
using Identity.Directory.Features.Role.Shared;
using Identity.Directory.Features.Role.UpdateRole;
using Identity.Directory.Features.User.AssignRoles;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Features;

[Trait("Category", "Integration")]
[Trait("Docker", "true")]
public sealed class RolesControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RolesControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
    }

    [Fact]
    public async Task CreateRole_Success()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        IntegrationAuthHelper.Authenticate(
            _client,
            tenantId: tenantId,
            role: "TenantAdmin",
            permissions: ["Roles.Create", "Roles.Read"]
        );

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
        var tenantId = Guid.NewGuid();
        IntegrationAuthHelper.Authenticate(
            _client,
            tenantId: tenantId,
            role: "TenantAdmin",
            permissions: ["Roles.Create", "Roles.Update", "Roles.Read"]
        );

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
        var tenantId = Guid.NewGuid();
        IntegrationAuthHelper.Authenticate(
            _client,
            tenantId: tenantId,
            role: "TenantAdmin",
            permissions: ["Roles.Create", "Roles.Delete", "Roles.Read"]
        );

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
        var (tenant, user) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            "assign_role_test",
            "assign_role_test@test.com",
            ct: ct
        );

        IntegrationAuthHelper.Authenticate(
            _client,
            tenantId: tenant.Id,
            role: "TenantAdmin",
            permissions: ["Roles.Create", "Users.Update", "Roles.Read"]
        );

        var createReq = new CreateRoleRequest("Assigned Role", ["Some.Perm"]);
        var createRes = await _client.PostAsJsonAsync("/api/v1/roles", createReq, ct);
        var createdRole = await createRes.Content.ReadFromJsonAsync<RoleResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );

        var assignReq = new AssignUserRolesRequest([createdRole!.Id]);
        var assignRes = await _client.PostAsJsonAsync(
            $"/api/v1/users/{user.Id}/roles",
            assignReq,
            ct
        );

        assignRes.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
