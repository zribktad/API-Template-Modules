using Asp.Versioning;
using ErrorOr;
using Identity.Api.Authorization;
using Identity.Api.ErrorOrMapping;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Wolverine;

namespace Identity.Api.Controllers.V1;

[ApiVersion(1.0)]
public sealed class TenantsController(IMessageBus bus) : ApiControllerBase
{
    [HttpGet]
    [RequirePermission(Permission.Tenants.Read)]
    [OutputCache(PolicyName = CacheTags.Tenants)]
    public async Task<ActionResult<PagedResponse<TenantResponse>>> GetAll(
        [FromQuery] TenantFilter filter,
        CancellationToken ct
    )
    {
        ErrorOr<PagedResponse<TenantResponse>> result = await bus.InvokeAsync<ErrorOr<PagedResponse<TenantResponse>>>(
            new GetTenantsQuery(filter),
            ct
        );
        return result.ToActionResult(this);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.Tenants.Read)]
    [OutputCache(PolicyName = CacheTags.Tenants)]
    public async Task<ActionResult<TenantResponse>> GetById(Guid id, CancellationToken ct)
    {
        ErrorOr<TenantResponse> result = await bus.InvokeAsync<ErrorOr<TenantResponse>>(
            new GetTenantByIdQuery(id),
            ct
        );
        return result.ToActionResult(this);
    }

    [HttpPost]
    [RequirePermission(Permission.Tenants.Create)]
    public async Task<ActionResult<TenantResponse>> Create(CreateTenantRequest request, CancellationToken ct)
    {
        ErrorOr<TenantResponse> result = await bus.InvokeAsync<ErrorOr<TenantResponse>>(
            new CreateTenantCommand(request),
            ct
        );
        return result.ToCreatedResult(this, v => new { id = v.Id, version = this.GetApiVersion() });
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.Tenants.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        ErrorOr<Success> result = await bus.InvokeAsync<ErrorOr<Success>>(
            new DeleteTenantCommand(id),
            ct
        );
        return result.ToNoContentResult(this);
    }
}
