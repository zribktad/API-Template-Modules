using Asp.Versioning;
using BuildingBlocks.Web.Api;
using ErrorOr;
using Identity.Auth.Security;
using Identity.Directory.Features.Account;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Identity.Directory.Controllers.V1;

/// <summary>Account security operations for the signed-in human user (BFF cookie or bearer JWT).</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/account")]
[Authorize(
    AuthenticationSchemes = $"{AuthConstants.BffSchemes.Cookie},{JwtBearerDefaults.AuthenticationScheme}"
)]
public sealed class AccountController(IMessageBus bus, ICurrentRequestUser currentUser)
    : ControllerBase
{
    /// <summary>Changes the Keycloak password and terminates all sessions (IdP + BFF).</summary>
    [HttpPost("password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken ct
    )
    {
        if (
            !TryGetInteractiveKeycloakIdentity(
                out string keycloakUserId,
                out string preferredUsername
            )
        )
            return Forbid();

        ErrorOr<Success> result = await bus.InvokeAsync<ErrorOr<Success>>(
            new ChangeOwnPasswordCommand(keycloakUserId, preferredUsername, request),
            ct
        );
        return result.ToNoContentResult(this);
    }

    /// <summary>Logs the user out of Keycloak everywhere and revokes all BFF sessions for this account.</summary>
    [HttpPost("sessions/revoke-all")]
    public async Task<IActionResult> RevokeAllSessions(CancellationToken ct)
    {
        if (!TryGetInteractiveKeycloakIdentity(out string keycloakUserId, out _))
            return Forbid();

        ErrorOr<Success> result = await bus.InvokeAsync<ErrorOr<Success>>(
            new RevokeAllOwnSessionsCommand(keycloakUserId),
            ct
        );
        return result.ToNoContentResult(this);
    }

    private bool TryGetInteractiveKeycloakIdentity(
        out string keycloakUserId,
        out string preferredUsername
    )
    {
        keycloakUserId = string.Empty;
        preferredUsername = string.Empty;

        if (!currentUser.IsInteractiveUser)
            return false;

        string? sub = currentUser.OidcSubject;
        string? un = currentUser.PreferredUsername;

        if (string.IsNullOrEmpty(sub) || string.IsNullOrEmpty(un))
            return false;

        keycloakUserId = sub;
        preferredUsername = un;
        return true;
    }
}
