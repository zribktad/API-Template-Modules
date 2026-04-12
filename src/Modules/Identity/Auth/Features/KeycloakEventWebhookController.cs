using ErrorOr;
using Identity.Auth.Options;
using Identity.Directory.Features.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using SharedKernel.Contracts.Api;
using Wolverine;

namespace Identity.Auth.Controllers;

/// <summary>Inbound webhook for Keycloak HTTP event listeners (e.g. after password update via email).</summary>
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[AllowAnonymous]
[Route("internal/keycloak-events")]
public sealed class KeycloakEventWebhookController(
    IMessageBus bus,
    IOptions<KeycloakOptions> keycloakOptions
) : ControllerBase
{
    /// <summary>
    ///     Invoked when a user's password was changed outside this API (e.g. Keycloak required action).
    ///     Secured with a shared secret header when <see cref="KeycloakEventWebhookOptions.ApiKey"/> is configured.
    /// </summary>
    [HttpPost("password-changed")]
    public async Task<IActionResult> PasswordChanged(
        [FromBody] KeycloakPasswordChangedWebhookRequest? body,
        CancellationToken ct
    )
    {
        KeycloakEventWebhookOptions webhook = keycloakOptions.Value.EventWebhook;
        if (string.IsNullOrEmpty(webhook.ApiKey))
            return NotFound();

        string headerName = string.IsNullOrWhiteSpace(webhook.ApiKeyHeaderName)
            ? KeycloakEventWebhookOptions.DefaultApiKeyHeaderName
            : webhook.ApiKeyHeaderName;

        if (
            !Request.Headers.TryGetValue(headerName, out StringValues provided)
            || provided.Count != 1
            || !string.Equals(provided[0], webhook.ApiKey, StringComparison.Ordinal)
        )
            return Unauthorized();

        if (body is null || string.IsNullOrWhiteSpace(body.KeycloakUserId))
            return BadRequest();

        ErrorOr<Success> result = await bus.InvokeAsync<ErrorOr<Success>>(
            new KeycloakPasswordChangedWebhookCommand(body.KeycloakUserId.Trim()),
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
