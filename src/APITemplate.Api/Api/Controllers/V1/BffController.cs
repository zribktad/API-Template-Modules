using System.Security.Claims;
using APITemplate.Api.Controllers;
using APITemplate.Application.Common.Options;
using APITemplate.Application.Common.Security;
using APITemplate.Application.Features.Bff.DTOs;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace APITemplate.Api.Controllers.V1;

[ApiVersion(1.0)]
[Authorize(AuthenticationSchemes = AuthConstants.BffSchemes.Cookie)]
/// <summary>
/// Presentation-layer controller that exposes Backend-for-Frontend (BFF) endpoints for
/// cookie-based browser clients, including login, logout, CSRF token retrieval, and current-user info.
/// </summary>
public sealed class BffController : ApiControllerBase
{
    private readonly BffOptions _bffOptions;

    public BffController(IOptions<BffOptions> bffOptions)
    {
        _bffOptions = bffOptions.Value;
    }

    /// <summary>
    /// Initiates an OIDC authorization-code challenge, redirecting the browser to Keycloak.
    /// Falls back to the root path when <paramref name="returnUrl"/> is not a local URL.
    /// </summary>
    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login([FromQuery] string? returnUrl = null)
    {
        var redirectUri = Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
        return Challenge(
            new AuthenticationProperties { RedirectUri = redirectUri },
            AuthConstants.BffSchemes.Oidc
        );
    }

    /// <summary>
    /// Signs the user out of both the cookie session and the OIDC provider, then redirects
    /// to the configured post-logout URI.
    /// </summary>
    [HttpGet("logout")]
    public IActionResult Logout()
    {
        return SignOut(
            new AuthenticationProperties { RedirectUri = _bffOptions.PostLogoutRedirectUri },
            AuthConstants.BffSchemes.Cookie,
            AuthConstants.BffSchemes.Oidc
        );
    }

    /// <summary>
    /// Returns the CSRF header name and a static token value that browser clients must include
    /// on every state-changing request made with the cookie authentication scheme.
    /// </summary>
    [HttpGet("csrf")]
    [AllowAnonymous]
    public IActionResult GetCsrf() =>
        Ok(
            new
            {
                headerName = AuthConstants.Csrf.HeaderName,
                headerValue = AuthConstants.Csrf.HeaderValue,
            }
        );

    /// <summary>
    /// Returns the authenticated user's identity claims (id, username, email, tenant, roles)
    /// extracted from the current cookie session.
    /// </summary>
    [HttpGet("user")]
    public IActionResult GetUser()
    {
        var user = HttpContext.User;

        var result = new BffUserResponse(
            UserId: user.FindFirstValue(ClaimTypes.NameIdentifier),
            Username: user.FindFirstValue(ClaimTypes.Name),
            Email: user.FindFirstValue(ClaimTypes.Email),
            TenantId: user.FindFirstValue(AuthConstants.Claims.TenantId),
            Roles: user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray()
        );

        return Ok(result);
    }
}
