using APITemplate.Api.Authorization;
using APITemplate.Api.Controllers;
using APITemplate.Api.ErrorOrMapping;
using APITemplate.Application.Common.DTOs;
using APITemplate.Application.Common.Events;
using APITemplate.Application.Common.Security;
using APITemplate.Application.Features.TenantInvitation;
using APITemplate.Application.Features.TenantInvitation.DTOs;
using Asp.Versioning;
using ErrorOr;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Wolverine;

namespace APITemplate.Api.Controllers.V1;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/tenant-invitations")]
/// <summary>
/// Presentation-layer controller that manages the lifecycle of tenant invitations,
/// including creation, acceptance via a token link, revocation, and resending.
/// </summary>
public sealed class TenantInvitationsController(IMessageBus bus) : ApiControllerBase
{
    /// <summary>Returns a paginated list of tenant invitations, optionally filtered.</summary>
    [HttpGet]
    [RequirePermission(Permission.Invitations.Read)]
    [OutputCache(PolicyName = CacheTags.TenantInvitations)]
    public async Task<ActionResult<PagedResponse<TenantInvitationResponse>>> GetAll(
        [FromQuery] TenantInvitationFilter filter,
        CancellationToken ct
    )
    {
        var result = await bus.InvokeAsync<ErrorOr<PagedResponse<TenantInvitationResponse>>>(
            new GetTenantInvitationsQuery(filter),
            ct
        );
        return result.ToActionResult(this);
    }

    /// <summary>Creates a new tenant invitation and sends the invite email.</summary>
    [HttpPost]
    [RequirePermission(Permission.Invitations.Create)]
    public async Task<ActionResult<TenantInvitationResponse>> Create(
        CreateTenantInvitationRequest request,
        CancellationToken ct
    )
    {
        var result = await bus.InvokeAsync<ErrorOr<TenantInvitationResponse>>(
            new CreateTenantInvitationCommand(request),
            ct
        );
        if (result.IsError)
            return result.ToActionResult(this);

        return CreatedAtAction(
            nameof(GetAll),
            new { version = this.GetApiVersion() },
            result.Value
        );
    }

    /// <summary>Accepts a pending invitation using the one-time token from the invite email; allows anonymous callers.</summary>
    [HttpPost("accept")]
    [AllowAnonymous]
    public async Task<IActionResult> Accept(
        [FromBody] AcceptInvitationRequest request,
        CancellationToken ct
    )
    {
        var result = await bus.InvokeAsync<ErrorOr<Success>>(
            new AcceptTenantInvitationCommand(request.Token),
            ct
        );
        return result.ToOkResult(this);
    }

    /// <summary>Marks an outstanding invitation as revoked so the token can no longer be accepted.</summary>
    [HttpPatch("{id:guid}/revoke")]
    [RequirePermission(Permission.Invitations.Revoke)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<ErrorOr<Success>>(
            new RevokeTenantInvitationCommand(id),
            ct
        );
        return result.ToNoContentResult(this);
    }

    /// <summary>Re-sends the invitation email for a pending invitation that has not yet been accepted or revoked.</summary>
    [HttpPost("{id:guid}/resend")]
    [RequirePermission(Permission.Invitations.Create)]
    public async Task<IActionResult> Resend(Guid id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<ErrorOr<Success>>(
            new ResendTenantInvitationCommand(id),
            ct
        );
        return result.ToOkResult(this);
    }
}
