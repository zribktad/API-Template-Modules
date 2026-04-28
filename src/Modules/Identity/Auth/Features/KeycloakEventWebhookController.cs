using ErrorOr;
using Identity.Auth.Security;
using Identity.Directory.Features.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.Contracts.Api;
using Wolverine;

namespace Identity.Auth.Controllers;

/// <summary>Inbound webhook for Keycloak HTTP event listeners (e.g. after password update via email).</summary>
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("internal/keycloak-events")]
[Authorize(AuthenticationSchemes = AuthConstants.WebhookSchemes.KeycloakEvent)]
public sealed class KeycloakEventWebhookController(IMessageBus bus) : ControllerBase
{
    /// <summary>
    ///     Invoked when a user's password was changed outside this API (e.g. Keycloak required action).
    ///     Authentication is enforced by <see cref="KeycloakWebhookAuthenticationHandler"/>.
    /// </summary>
    [HttpPost("password-changed")]
    public async Task<IActionResult> PasswordChanged(
        [FromBody] KeycloakPasswordChangedWebhookRequest body,
        CancellationToken ct
    )
    {
        ErrorOr<Success> result = await bus.InvokeAsync<ErrorOr<Success>>(
            new KeycloakPasswordChangedWebhookCommand(body.KeycloakUserId?.Trim() ?? string.Empty),
            ct
        );

        return result.ToNoContentResult(this);
    }
}

/// <summary>Payload from the Keycloak HTTP event sender.</summary>
public sealed class KeycloakPasswordChangedWebhookRequest
{
    /// <summary>Keycloak realm user id (UUID from the Admin API / Users resource).</summary>
    public required string KeycloakUserId { get; init; }
}
