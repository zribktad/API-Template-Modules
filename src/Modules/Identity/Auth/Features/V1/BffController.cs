using System.Security.Claims;
using Asp.Versioning;
using Identity.Auth.Features.Bff.DTOs;
using Identity.Auth.Options;
using Identity.Auth.Security.Sessions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SharedKernel.Contracts.Queries.Identity;
using Wolverine;

namespace Identity.Auth.Controllers.V1;

[ApiVersion(1.0)]
[Authorize(AuthenticationSchemes = AuthConstants.BffSchemes.Cookie)]
public sealed class BffController : ApiControllerBase
{
    private readonly BffOptions _bffOptions;
    private readonly IBffCsrfTokenService _csrfTokens;
    private readonly IMessageBus _bus;
    private readonly IReadOnlyDictionary<string, IExternalIdentityProvider> _identityProviders;
    private readonly IReadOnlyList<ExternalProviderResponse> _externalProviderResponses;

    public BffController(
        IOptions<BffOptions> bffOptions,
        IBffCsrfTokenService csrfTokens,
        IMessageBus bus,
        IEnumerable<IExternalIdentityProvider> identityProviders
    )
    {
        _bffOptions = bffOptions.Value;
        _csrfTokens = csrfTokens;
        _bus = bus;
        _identityProviders = identityProviders.ToDictionary(
            p => p.IdpHint,
            p => p,
            StringComparer.OrdinalIgnoreCase
        );
        _externalProviderResponses = _identityProviders
            .Values.Select(p => new ExternalProviderResponse(p.IdpHint, p.DisplayName))
            .ToList();
    }

    [HttpPost("login/ldap")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginLdap([FromBody] LdapLoginRequest request)
    {
        ErrorOr.ErrorOr<LdapUserContract> result =
            await _bus.InvokeAsync<ErrorOr.ErrorOr<LdapUserContract>>(
                new AuthenticateLdapQuery(request.Username, request.Password)
            );

        if (result.IsError)
        {
            return result.ToErrorResult(this);
        }

        LdapUserContract ldapUser = result.Value;
        List<Claim> claims = new()
        {
            new Claim(AuthConstants.Claims.Subject, ldapUser.LocalId.ToString()),
            new Claim(AuthConstants.Claims.TenantId, AuthConstants.Tenants.Bootstrap),
            new Claim(ClaimTypes.Name, ldapUser.DisplayName ?? ldapUser.Username),
            new Claim("display_name", ldapUser.DisplayName ?? ldapUser.Username),
            new Claim("auth_method", "ldap"),
        };

        if (!string.IsNullOrEmpty(ldapUser.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, ldapUser.Email));
        }

        ClaimsIdentity identity = new(claims, AuthConstants.BffSchemes.Cookie);
        ClaimsPrincipal principal = new(identity);

        await HttpContext.SignInAsync(
            AuthConstants.BffSchemes.Cookie,
            principal,
            new AuthenticationProperties { IsPersistent = true }
        );

        return Ok();
    }

    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login([FromQuery] string? returnUrl = null)
    {
        string redirectUri = Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
        return Challenge(
            new AuthenticationProperties { RedirectUri = redirectUri },
            AuthConstants.BffSchemes.Oidc
        );
    }

    [HttpGet("login/{idpHint}")]
    [AllowAnonymous]
    public IActionResult LoginWithProvider(string idpHint, [FromQuery] string? returnUrl = null)
    {
        if (
            !_identityProviders.TryGetValue(
                idpHint,
                out IExternalIdentityProvider? identityProvider
            )
        )
            return NotFound();

        string redirectUri = Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
        AuthenticationProperties properties = new() { RedirectUri = redirectUri };
        properties.Items[AuthConstants.KeycloakAuthProperties.IdpHint] = identityProvider.IdpHint;

        return Challenge(properties, AuthConstants.BffSchemes.Oidc);
    }

    [HttpGet("external-providers")]
    [AllowAnonymous]
    public IActionResult GetExternalProviders()
    {
        return Ok(_externalProviderResponses);
    }

    [HttpGet("logout")]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        return SignOut(
            new AuthenticationProperties { RedirectUri = _bffOptions.PostLogoutRedirectUri },
            AuthConstants.BffSchemes.Cookie,
            AuthConstants.BffSchemes.Oidc
        );
    }

    [HttpGet("csrf")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCsrf()
    {
        AuthenticateResult auth = await HttpContext.AuthenticateAsync(
            AuthConstants.BffSchemes.Cookie
        );
        if (auth.Succeeded && auth.Properties.TryGetBffSessionId(out string? sessionId))
        {
            return Ok(
                new
                {
                    headerName = AuthConstants.Csrf.HeaderName,
                    headerValue = _csrfTokens.CreateToken(sessionId),
                    tokenFormat = AuthConstants.Csrf.TokenFormats.DataProtection,
                }
            );
        }

        return Unauthorized();
    }

    [HttpGet("user")]
    public IActionResult GetUser()
    {
        ClaimsPrincipal user = HttpContext.User;
        BffUserResponse result = new(
            user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue(AuthConstants.Claims.Subject),
            user.FindFirstValue(ClaimTypes.Name),
            user.FindFirstValue(ClaimTypes.Email),
            user.FindFirstValue(AuthConstants.Claims.TenantId),
            user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray()
        );
        return Ok(result);
    }
}
