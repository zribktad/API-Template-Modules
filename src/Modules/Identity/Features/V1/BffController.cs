using System.Security.Claims;
using Asp.Versioning;
using Identity.Features.Bff.DTOs;
using Identity.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Identity.Controllers.V1;

[ApiVersion(1.0)]
[Authorize(AuthenticationSchemes = AuthConstants.BffSchemes.Cookie)]
public sealed class BffController : ApiControllerBase
{
    private readonly BffOptions _bffOptions;
    private readonly IReadOnlyDictionary<string, IExternalIdentityProvider> _identityProviders;

    public BffController(
        IOptions<BffOptions> bffOptions,
        IEnumerable<IExternalIdentityProvider> identityProviders
    )
    {
        _bffOptions = bffOptions.Value;
        _identityProviders = identityProviders.ToDictionary(
            p => p.IdpHint,
            p => p,
            StringComparer.OrdinalIgnoreCase
        );
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
        if (!_identityProviders.ContainsKey(idpHint))
            return NotFound();

        string redirectUri = Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
        AuthenticationProperties properties = new() { RedirectUri = redirectUri };
        properties.Items["kc_idp_hint"] = idpHint;

        return Challenge(properties, AuthConstants.BffSchemes.Oidc);
    }

    [HttpGet("external-providers")]
    [AllowAnonymous]
    public IActionResult GetExternalProviders()
    {
        IEnumerable<object> providers = _identityProviders.Values.Select(p => new
        {
            idpHint = p.IdpHint,
            displayName = p.DisplayName,
        });
        return Ok(providers);
    }

    [HttpGet("logout")]
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
    public IActionResult GetCsrf()
    {
        return Ok(
            new
            {
                headerName = AuthConstants.Csrf.HeaderName,
                headerValue = AuthConstants.Csrf.HeaderValue,
            }
        );
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
