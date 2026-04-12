using System.Net;
using System.Net.Http.Json;
using APITemplate.Tests.Integration.Helpers;
using Identity.Directory.Features.Role.CreateRole;
using Identity.Directory.Features.Role.Shared;
using Identity.Directory.Features.Role.UpdateRole;
using Identity.Directory.Features.User.AssignRoles;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Features;

[Trait("Category", "Integration.Docker")]
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
        var tenantId = Guid.NewGuid();
        IntegrationAuthHelper.Authenticate(
            _client,
            tenantId: tenantId,
            role: "TenantAdmin",
            permissions: new[] { "Roles.Create", "Roles.Read" }
        );

        var request = new CreateRoleRequest("Test Role", new List<string> { "Test.Permission" });

        var response = await _client.PostAsJsonAsync("/api/v1/roles", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var createdRole = await response.Content.ReadFromJsonAsync<RoleResponse>();
        createdRole.ShouldNotBeNull();
        createdRole.Name.ShouldBe("Test Role");
        createdRole.Permissions.ShouldContain("Test.Permission");

        // Verify we can fetch it
        var listResponse = await _client.GetAsync("/api/v1/roles");
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var roles = await listResponse.Content.ReadFromJsonAsync<List<RoleResponse>>();
        roles.ShouldNotBeNull();
        roles.ShouldContain(r => r.Id == createdRole.Id);
    }

    [Fact]
    public async Task UpdateRole_Success()
    {
        var tenantId = Guid.NewGuid();
        IntegrationAuthHelper.Authenticate(
            _client,
            tenantId: tenantId,
            role: "TenantAdmin",
            permissions: new[] { "Roles.Create", "Roles.Update", "Roles.Read" }
        );

        var createReq = new CreateRoleRequest("To Update", new List<string>());
        var createRes = await _client.PostAsJsonAsync("/api/v1/roles", createReq);
        var createdRole = await createRes.Content.ReadFromJsonAsync<RoleResponse>();

        var updateReq = new UpdateRoleRequest("Updated", new List<string> { "New.Perm" });
        var updateRes = await _client.PutAsJsonAsync($"/api/v1/roles/{createdRole!.Id}", updateReq);

        updateRes.StatusCode.ShouldBe(HttpStatusCode.OK);

        var listResponse = await _client.GetAsync("/api/v1/roles");
        var roles = await listResponse.Content.ReadFromJsonAsync<List<RoleResponse>>();
        roles!.First(r => r.Id == createdRole.Id).Name.ShouldBe("Updated");
        roles!.First(r => r.Id == createdRole.Id).Permissions.ShouldContain("New.Perm");
    }

    [Fact]
    public async Task DeleteRole_Success()
    {
        var tenantId = Guid.NewGuid();
        IntegrationAuthHelper.Authenticate(
            _client,
            tenantId: tenantId,
            role: "TenantAdmin",
            permissions: new[] { "Roles.Create", "Roles.Delete", "Roles.Read" }
        );

        var createReq = new CreateRoleRequest("To Delete", new List<string>());
        var createRes = await _client.PostAsJsonAsync("/api/v1/roles", createReq);
        var createdRole = await createRes.Content.ReadFromJsonAsync<RoleResponse>();

        var deleteRes = await _client.DeleteAsync($"/api/v1/roles/{createdRole!.Id}");
        deleteRes.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var listResponse = await _client.GetAsync("/api/v1/roles");
        var roles = await listResponse.Content.ReadFromJsonAsync<List<RoleResponse>>();
        roles!.ShouldNotContain(r => r.Id == createdRole.Id);
    }

    [Fact]
    public async Task AssignUserRoles_Success()
    {
        var (tenant, user) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            "assign_role_test",
            "assign_role_test@test.com"
        );

        IntegrationAuthHelper.Authenticate(
            _client,
            tenantId: tenant.Id,
            role: "TenantAdmin",
            permissions: new[] { "Roles.Create", "Users.Update", "Roles.Read" }
        );

        var createReq = new CreateRoleRequest("Assigned Role", new List<string> { "Some.Perm" });
        var createRes = await _client.PostAsJsonAsync("/api/v1/roles", createReq);
        var createdRole = await createRes.Content.ReadFromJsonAsync<RoleResponse>();

        var assignReq = new AssignUserRolesRequest(new List<Guid> { createdRole!.Id });
        var assignRes = await _client.PostAsJsonAsync($"/api/v1/users/{user.Id}/roles", assignReq);

        assignRes.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
