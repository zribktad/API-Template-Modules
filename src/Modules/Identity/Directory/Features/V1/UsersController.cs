using Asp.Versioning;
using ErrorOr;
using Identity.Auth.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Wolverine;

namespace Identity.Directory.Controllers.V1;

[ApiVersion(1.0)]
public sealed class UsersController(IMessageBus bus, ICurrentRequestUser currentUser)
    : ApiControllerBase
{
    [HttpGet]
    [RequirePermission(Permission.Users.Read)]
    [OutputCache(PolicyName = CacheTags.Users)]
    public async Task<ActionResult<PagedResponse<UserResponse>>> GetAll(
        [FromQuery] UserFilter filter,
        CancellationToken ct
    )
    {
        ErrorOr<PagedResponse<UserResponse>> result = await bus.InvokeAsync<
            ErrorOr<PagedResponse<UserResponse>>
        >(new GetUsersQuery(filter), ct);
        return result.ToActionResult(this);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.Users.Read)]
    [OutputCache(PolicyName = CacheTags.Users)]
    public async Task<ActionResult<UserResponse>> GetById(Guid id, CancellationToken ct)
    {
        ErrorOr<UserResponse> result = await bus.InvokeAsync<ErrorOr<UserResponse>>(
            new GetUserByIdQuery(id),
            ct
        );
        return result.ToActionResult(this);
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> GetMe(CancellationToken ct)
    {
        if (currentUser.ApplicationUserId is Guid appUserId)
        {
            ErrorOr<UserResponse> byAppId = await bus.InvokeAsync<ErrorOr<UserResponse>>(
                new GetUserByIdQuery(appUserId),
                ct
            );
            return byAppId.ToActionResult(this);
        }

        if (!string.IsNullOrEmpty(currentUser.OidcSubject))
        {
            ErrorOr<UserResponse> byKc = await bus.InvokeAsync<ErrorOr<UserResponse>>(
                new GetUserByKeycloakUserIdQuery(currentUser.OidcSubject),
                ct
            );
            return byKc.ToActionResult(this);
        }

        return Unauthorized();
    }

    [HttpPost]
    [RequirePermission(Permission.Users.Create)]
    public async Task<ActionResult<UserResponse>> Create(
        CreateUserRequest request,
        CancellationToken ct
    )
    {
        ErrorOr<UserResponse> result = await bus.InvokeAsync<ErrorOr<UserResponse>>(
            new CreateUserCommand(request),
            ct
        );
        return result.ToCreatedResult(
            this,
            nameof(GetById),
            v => new { id = v.Id, version = this.GetApiVersion() }
        );
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permission.Users.Update)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateUserRequest request,
        CancellationToken ct
    )
    {
        ErrorOr<Success> result = await bus.InvokeAsync<ErrorOr<Success>>(
            new UpdateUserCommand(id, request),
            ct
        );
        return result.ToNoContentResult(this);
    }

    [HttpPatch("{id:guid}/activate")]
    [RequirePermission(Permission.Users.Update)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        ErrorOr<Success> result = await bus.InvokeAsync<ErrorOr<Success>>(
            new SetUserActiveCommand(id, true),
            ct
        );
        return result.ToNoContentResult(this);
    }

    [HttpPatch("{id:guid}/deactivate")]
    [RequirePermission(Permission.Users.Update)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        ErrorOr<Success> result = await bus.InvokeAsync<ErrorOr<Success>>(
            new SetUserActiveCommand(id, false),
            ct
        );
        return result.ToNoContentResult(this);
    }

    [HttpPost("{id:guid}/roles")]
    [RequirePermission(Permission.Users.Update)]
    public async Task<IActionResult> AssignRoles(
        Guid id,
        Identity.Directory.Features.User.AssignRoles.AssignUserRolesRequest request,
        CancellationToken ct
    )
    {
        ErrorOr<Success> result = await bus.InvokeAsync<ErrorOr<Success>>(
            new Identity.Directory.Features.User.AssignRoles.AssignUserRolesCommand(id, request),
            ct
        );
        return result.ToNoContentResult(this);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.Users.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        ErrorOr<Success> result = await bus.InvokeAsync<ErrorOr<Success>>(
            new DeleteUserCommand(id),
            ct
        );
        return result.ToNoContentResult(this);
    }

    [HttpPost("password-reset")]
    [AllowAnonymous]
    public async Task<IActionResult> RequestPasswordReset(
        RequestPasswordResetRequest request,
        CancellationToken ct
    )
    {
        ErrorOr<Success> result = await bus.InvokeAsync<ErrorOr<Success>>(
            new KeycloakPasswordResetCommand(request),
            ct
        );
        return result.ToOkResult(this);
    }
}
