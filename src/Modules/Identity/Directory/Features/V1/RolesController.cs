using Asp.Versioning;
using ErrorOr;
using Identity.Directory.Features.Role.CreateRole;
using Identity.Directory.Features.Role.DeleteRole;
using Identity.Directory.Features.Role.GetPermissions;
using Identity.Directory.Features.Role.GetRole;
using Identity.Directory.Features.Role.GetRoles;
using Identity.Directory.Features.Role.Shared;
using Identity.Directory.Features.Role.UpdateRole;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.Contracts.Security;
using Wolverine;

namespace Identity.Directory.Controllers.V1;

[ApiVersion(1.0)]
public sealed class RolesController(IMessageBus bus) : ApiControllerBase
{
    [HttpPost]
    [RequirePermission(Permission.Roles.Create)]
    public async Task<ActionResult<RoleResponse>> Create(
        CreateRoleRequest request,
        CancellationToken ct
    )
    {
        ErrorOr<RoleResponse> result = await bus.InvokeAsync<ErrorOr<RoleResponse>>(
            new CreateRoleCommand(request),
            ct
        );
        return result.ToCreatedResult(
            this,
            nameof(GetRole),
            v => new { id = v.Id, version = this.GetApiVersion() }
        );
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permission.Roles.Update)]
    public async Task<ActionResult<RoleResponse>> Update(
        Guid id,
        UpdateRoleRequest request,
        CancellationToken ct
    )
    {
        ErrorOr<RoleResponse> result = await bus.InvokeAsync<ErrorOr<RoleResponse>>(
            new UpdateRoleCommand(id, request),
            ct
        );
        return result.ToActionResult(this);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.Roles.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        ErrorOr<Success> result = await bus.InvokeAsync<ErrorOr<Success>>(
            new DeleteRoleCommand(id),
            ct
        );
        return result.ToNoContentResult(this);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.Roles.Read)]
    public async Task<ActionResult<RoleResponse>> GetRole(Guid id, CancellationToken ct)
    {
        ErrorOr<RoleResponse> result = await bus.InvokeAsync<ErrorOr<RoleResponse>>(
            new GetRoleQuery(id),
            ct
        );
        return result.ToActionResult(this);
    }

    [HttpGet]
    [RequirePermission(Permission.Roles.Read)]
    public async Task<ActionResult<IReadOnlyList<RoleResponse>>> GetRoles(CancellationToken ct)
    {
        ErrorOr<IReadOnlyList<RoleResponse>> result = await bus.InvokeAsync<
            ErrorOr<IReadOnlyList<RoleResponse>>
        >(new GetRolesQuery(), ct);
        return result.ToActionResult(this);
    }

    [HttpGet("permissions")]
    [RequirePermission(Permission.Roles.Read)]
    public async Task<ActionResult<IReadOnlyList<string>>> GetPermissions(CancellationToken ct)
    {
        ErrorOr<IReadOnlyList<string>> result = await bus.InvokeAsync<
            ErrorOr<IReadOnlyList<string>>
        >(new GetPermissionsQuery(), ct);
        return result.ToActionResult(this);
    }
}
