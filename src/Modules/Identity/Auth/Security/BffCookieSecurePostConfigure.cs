using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Identity.Auth.Security;

/// <summary>
///     After <see cref="IdentityModule.AddIdentityModule" /> sets cookie defaults, enforce
///     <see cref="CookieSecurePolicy.Always" /> outside Development so session cookies are never
///     sent over plaintext HTTP in production deployments.
/// </summary>
public sealed class BffCookieSecurePostConfigure(IWebHostEnvironment environment)
    : IPostConfigureOptions<CookieAuthenticationOptions>
{
    public void PostConfigure(string? name, CookieAuthenticationOptions options)
    {
        if (!string.Equals(name, AuthConstants.BffSchemes.Cookie, StringComparison.Ordinal))
            return;

        if (!environment.IsDevelopment())
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    }
}
