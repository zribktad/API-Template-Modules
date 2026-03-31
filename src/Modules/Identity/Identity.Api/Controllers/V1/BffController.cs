using System.Security.Claims;
using Asp.Versioning;
using Identity.Application.Features.Bff.DTOs;
using Identity.Application.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Identity.Api.Controllers.V1;

[ApiVersion(1.0)]
[Authorize(AuthenticationSchemes = AuthConstants.BffSchemes.Cookie)]
public sealed class BffController : ApiControllerBase
{
    private readonly BffOptions _bffOptions;

    public BffController(IOptions<BffOptions> bffOptions)
    {
        _bffOptions = bffOptions.Value;
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
    public IActionResult GetCsrf() =>
        Ok(new
        {
            headerName = AuthConstants.Csrf.HeaderName,
            headerValue = AuthConstants.Csrf.HeaderValue,
        });

    [HttpGet("user")]
    public IActionResult GetUser()
    {
        System.Security.Claims.ClaimsPrincipal user = HttpContext.User;
        BffUserResponse result = new(
            UserId: user.FindFirstValue(ClaimTypes.NameIdentifier),
            Username: user.FindFirstValue(ClaimTypes.Name),
            Email: user.FindFirstValue(ClaimTypes.Email),
            TenantId: user.FindFirstValue(AuthConstants.Claims.TenantId),
            Roles: user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray()
        );
        return Ok(result);
    }
}
