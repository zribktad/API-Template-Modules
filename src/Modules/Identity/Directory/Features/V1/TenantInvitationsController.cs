using Asp.Versioning;
using ErrorOr;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Wolverine;

namespace Identity.Directory.Controllers.V1;

[ApiVersion(1.0)]
public sealed class TenantInvitationsController(IMessageBus bus) : ApiControllerBase
{
    [HttpGet]
    [RequirePermission(Permission.Invitations.Read)]
    [OutputCache(PolicyName = CacheTags.TenantInvitations)]
    public async Task<ActionResult<PagedResponse<TenantInvitationResponse>>> GetAll(
        [FromQuery] TenantInvitationFilter filter,
        CancellationToken ct
    )
    {
        ErrorOr<PagedResponse<TenantInvitationResponse>> result = await bus.InvokeAsync<
            ErrorOr<PagedResponse<TenantInvitationResponse>>
        >(new GetTenantInvitationsQuery(filter), ct);
        return result.ToActionResult(this);
    }

    [HttpPost]
    [RequirePermission(Permission.Invitations.Create)]
    public async Task<ActionResult<TenantInvitationResponse>> Create(
        CreateTenantInvitationRequest request,
        CancellationToken ct
    )
    {
        ErrorOr<TenantInvitationResponse> result = await bus.InvokeAsync<
            ErrorOr<TenantInvitationResponse>
        >(new CreateTenantInvitationCommand(request), ct);
        if (result.IsError)
            return result.ToActionResult(this);

        return CreatedAtAction(
            nameof(GetAll),
            new { version = this.GetApiVersion() },
            result.Value
        );
    }

    [HttpPost("accept")]
    [AllowAnonymous]
    public async Task<IActionResult> Accept(
        [FromBody] AcceptInvitationRequest request,
        CancellationToken ct
    )
    {
        ErrorOr<Success> result = await bus.InvokeAsync<ErrorOr<Success>>(
            new AcceptTenantInvitationCommand(request.Token),
            ct
        );
        return result.ToOkResult(this);
    }

    [HttpPatch("{id:guid}/revoke")]
    [RequirePermission(Permission.Invitations.Revoke)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        ErrorOr<Success> result = await bus.InvokeAsync<ErrorOr<Success>>(
            new RevokeTenantInvitationCommand(id),
            ct
        );
        return result.ToNoContentResult(this);
    }

    [HttpPost("{id:guid}/resend")]
    [RequirePermission(Permission.Invitations.Create)]
    public async Task<IActionResult> Resend(Guid id, CancellationToken ct)
    {
        ErrorOr<Success> result = await bus.InvokeAsync<ErrorOr<Success>>(
            new ResendTenantInvitationCommand(id),
            ct
        );
        return result.ToOkResult(this);
    }
}
