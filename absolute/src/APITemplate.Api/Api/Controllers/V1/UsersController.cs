using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using APITemplate.Api.Authorization;
using APITemplate.Api.Controllers;
using APITemplate.Api.ErrorOrMapping;
using APITemplate.Application.Common.DTOs;
using APITemplate.Application.Common.Events;
using APITemplate.Application.Common.Security;
using APITemplate.Application.Features.User.DTOs;
using Asp.Versioning;
using ErrorOr;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Wolverine;

namespace APITemplate.Api.Controllers.V1;

[ApiVersion(1.0)]
/// <summary>
/// Presentation-layer controller that exposes user management endpoints including
/// CRUD operations, activation/deactivation, role changes, and self-service password reset.
/// </summary>
public sealed class UsersController(IMessageBus bus) : ApiControllerBase
{
    /// <summary>Returns a paginated, filterable list of users.</summary>
    [HttpGet]
    [RequirePermission(Permission.Users.Read)]
    [OutputCache(PolicyName = CacheTags.Users)]
    public async Task<ActionResult<PagedResponse<UserResponse>>> GetAll(
        [FromQuery] UserFilter filter,
        CancellationToken ct
    )
    {
        var result = await bus.InvokeAsync<ErrorOr<PagedResponse<UserResponse>>>(
            new GetUsersQuery(filter),
            ct
        );
        return result.ToActionResult(this);
    }

    /// <summary>Returns a single user by their identifier, or 404 if not found.</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.Users.Read)]
    [OutputCache(PolicyName = CacheTags.Users)]
    public async Task<ActionResult<UserResponse>> GetById(Guid id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<ErrorOr<UserResponse>>(new GetUserByIdQuery(id), ct);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// Returns the currently authenticated user's profile by resolving their id from the
    /// JWT/cookie claims (<c>NameIdentifier</c>, <c>sub</c>, or a custom subject claim).
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> GetMe(CancellationToken ct)
    {
        var userId =
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(AuthConstants.Claims.Subject);

        if (userId is null || !Guid.TryParse(userId, out var id))
            return Unauthorized();

        var result = await bus.InvokeAsync<ErrorOr<UserResponse>>(new GetUserByIdQuery(id), ct);
        return result.ToActionResult(this);
    }

    /// <summary>Creates a new user account and returns it with a 201 Location header.</summary>
    [HttpPost]
    [RequirePermission(Permission.Users.Create)]
    public async Task<ActionResult<UserResponse>> Create(
        CreateUserRequest request,
        CancellationToken ct
    )
    {
        var result = await bus.InvokeAsync<ErrorOr<UserResponse>>(
            new CreateUserCommand(request),
            ct
        );
        return result.ToCreatedResult(this, v => new { id = v.Id, version = this.GetApiVersion() });
    }

    /// <summary>Replaces all mutable fields of an existing user.</summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(Permission.Users.Update)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateUserRequest request,
        CancellationToken ct
    )
    {
        var result = await bus.InvokeAsync<ErrorOr<Success>>(
            new UpdateUserCommand(id, request),
            ct
        );
        return result.ToNoContentResult(this);
    }

    /// <summary>Activates a previously deactivated user account.</summary>
    [HttpPatch("{id:guid}/activate")]
    [RequirePermission(Permission.Users.Update)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<ErrorOr<Success>>(
            new SetUserActiveCommand(id, IsActive: true),
            ct
        );
        return result.ToNoContentResult(this);
    }

    /// <summary>Deactivates an active user account, preventing further logins.</summary>
    [HttpPatch("{id:guid}/deactivate")]
    [RequirePermission(Permission.Users.Update)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<ErrorOr<Success>>(
            new SetUserActiveCommand(id, IsActive: false),
            ct
        );
        return result.ToNoContentResult(this);
    }

    /// <summary>Changes the role of an existing user within the current tenant.</summary>
    [HttpPatch("{id:guid}/role")]
    [RequirePermission(Permission.Users.Update)]
    public async Task<IActionResult> ChangeRole(
        Guid id,
        ChangeUserRoleRequest request,
        CancellationToken ct
    )
    {
        var result = await bus.InvokeAsync<ErrorOr<Success>>(
            new ChangeUserRoleCommand(id, request),
            ct
        );
        return result.ToNoContentResult(this);
    }

    /// <summary>Soft-deletes a user account by its identifier.</summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.Users.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<ErrorOr<Success>>(new DeleteUserCommand(id), ct);
        return result.ToNoContentResult(this);
    }

    /// <summary>
    /// Triggers a Keycloak-initiated password-reset email for the given address; allows
    /// anonymous callers so unauthenticated users can recover access.
    /// </summary>
    [HttpPost("password-reset")]
    [AllowAnonymous]
    public async Task<IActionResult> RequestPasswordReset(
        RequestPasswordResetRequest request,
        CancellationToken ct
    )
    {
        var result = await bus.InvokeAsync<ErrorOr<Success>>(
            new KeycloakPasswordResetCommand(request),
            ct
        );
        return result.ToOkResult(this);
    }
}
