using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using BuildingBlocks.Web.Api;
using ErrorOr;
using Identity.Auth.Options;
using Identity.Errors;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Identity.Auth.Security;

public sealed class KeycloakWebhookAuthenticationSchemeOptions : AuthenticationSchemeOptions;

/// <summary>
///     Authenticates inbound Keycloak event webhooks via the shared secret configured in
///     <see cref="KeycloakEventWebhookOptions"/>. Compares the configured key with the value of the
///     configured header in constant time. On failure, <see cref="HandleChallengeAsync"/> emits an
///     RFC 7807 ProblemDetails consistent with the rest of the API.
/// </summary>
public sealed class KeycloakWebhookAuthenticationHandler(
    IOptionsMonitor<KeycloakWebhookAuthenticationSchemeOptions> schemeOptions,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IOptionsMonitor<KeycloakOptions> keycloakOptions
)
    : AuthenticationHandler<KeycloakWebhookAuthenticationSchemeOptions>(
        schemeOptions,
        loggerFactory,
        encoder
    )
{
    private const string FailureCodeKey = "Identity.KeycloakWebhook.FailureCode";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        KeycloakEventWebhookOptions webhook = keycloakOptions.CurrentValue.EventWebhook;

        if (string.IsNullOrEmpty(webhook.ApiKey))
            return Task.FromResult(Fail(ErrorCatalog.KeycloakWebhook.Disabled));

        string headerName = string.IsNullOrWhiteSpace(webhook.ApiKeyHeaderName)
            ? KeycloakEventWebhookOptions.DefaultApiKeyHeaderName
            : webhook.ApiKeyHeaderName;

        if (
            !Request.Headers.TryGetValue(headerName, out StringValues provided)
            || provided.Count != 1
        )
            return Task.FromResult(Fail(ErrorCatalog.KeycloakWebhook.Unauthorized));

        byte[] expected = Encoding.UTF8.GetBytes(webhook.ApiKey);
        byte[] actual = Encoding.UTF8.GetBytes(provided[0]!);

        if (
            expected.Length != actual.Length
            || !CryptographicOperations.FixedTimeEquals(expected, actual)
        )
            return Task.FromResult(Fail(ErrorCatalog.KeycloakWebhook.Unauthorized));

        ClaimsIdentity identity = new(Scheme.Name);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "keycloak-webhook"));
        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        string code =
            Context.Items[FailureCodeKey] as string ?? ErrorCatalog.KeycloakWebhook.Unauthorized;
        Error error =
            code == ErrorCatalog.KeycloakWebhook.Disabled
                ? Error.NotFound(code, "Keycloak event webhook is disabled.")
                : Error.Unauthorized(code, "Webhook API key is missing or invalid.");

        ProblemDetails problem = error.ToProblemDetails(Context);
        Response.StatusCode = problem.Status ?? StatusCodes.Status401Unauthorized;

        IProblemDetailsService problemDetailsService =
            Context.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext { HttpContext = Context, ProblemDetails = problem }
        );
    }

    private AuthenticateResult Fail(string code)
    {
        Context.Items[FailureCodeKey] = code;
        return AuthenticateResult.Fail(code);
    }
}
