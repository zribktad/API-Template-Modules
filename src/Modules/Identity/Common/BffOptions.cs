using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Identity.Options;

/// <summary>
/// Configuration for the Backend-for-Frontend (BFF) session layer, including cookie settings,
/// requested OIDC scopes, and token refresh thresholds.
/// </summary>
public sealed class BffOptions
{
    [Description("Cookie name used to persist the BFF authenticated session.")]
    [Required]
    [MinLength(1)]
    public string CookieName { get; init; } = ".APITemplate.Auth";

    [Description("Relative or absolute URI used after the user signs out of the BFF session.")]
    [Required]
    [MinLength(1)]
    public string PostLogoutRedirectUri { get; init; } = "/";

    [Description("Maximum idle session lifetime, in minutes, for the BFF authentication cookie.")]
    [Range(1, int.MaxValue)]
    public int SessionTimeoutMinutes { get; init; } = 60;

    [Description("OIDC scopes requested during sign-in.")]
    [Required]
    [MinLength(1)]
    public string[] Scopes { get; init; } = [.. AuthConstants.Scopes.Default];

    [Description("Minutes before access token expiry when proactive refresh should begin.")]
    [Range(0, int.MaxValue)]
    public int TokenRefreshThresholdMinutes { get; init; } = 2;
}
