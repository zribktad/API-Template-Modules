using APITemplate.Api.Authorization;
using APITemplate.Api.Controllers;
using APITemplate.Api.ErrorOrMapping;
using APITemplate.Application.Common.Events;
using APITemplate.Application.Common.Security;
using Asp.Versioning;
using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Wolverine;

namespace APITemplate.Api.Controllers.V1;

[ApiVersion(1.0)]
/// <summary>
/// Presentation-layer controller that exposes CRUD endpoints for tenant management,
/// restricted to platform-level permissions with tenant-isolated output caching.
/// </summary>
public sealed class TenantsController(IMessageBus bus) : ApiControllerBase
{
    /// <summary>Returns a paginated, filterable list of tenants.</summary>
    [HttpGet]
    [RequirePermission(Permission.Tenants.Read)]
    [OutputCache(PolicyName = CacheTags.Tenants)]
    public async Task<ActionResult<PagedResponse<TenantResponse>>> GetAll(
        [FromQuery] TenantFilter filter,
        CancellationToken ct
    )
    {
        var result = await bus.InvokeAsync<ErrorOr<PagedResponse<TenantResponse>>>(
            new GetTenantsQuery(filter),
            ct
        );
        return result.ToActionResult(this);
    }

    /// <summary>Returns a single tenant by its identifier, or 404 if not found.</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.Tenants.Read)]
    [OutputCache(PolicyName = CacheTags.Tenants)]
    public async Task<ActionResult<TenantResponse>> GetById(Guid id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<ErrorOr<TenantResponse>>(new GetTenantByIdQuery(id), ct);
        return result.ToActionResult(this);
    }

    /// <summary>Creates a new tenant and returns it with a 201 Location header.</summary>
    [HttpPost]
    [RequirePermission(Permission.Tenants.Create)]
    public async Task<ActionResult<TenantResponse>> Create(
        CreateTenantRequest request,
        CancellationToken ct
    )
    {
        var result = await bus.InvokeAsync<ErrorOr<TenantResponse>>(
            new CreateTenantCommand(request),
            ct
        );
        return result.ToCreatedResult(this, v => new { id = v.Id, version = this.GetApiVersion() });
    }

    /// <summary>Soft-deletes a tenant and cascades the deletion to its child entities.</summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.Tenants.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<ErrorOr<Success>>(new DeleteTenantCommand(id), ct);
        return result.ToNoContentResult(this);
    }
}
