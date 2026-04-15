using System.Security.Claims;

namespace Identity.Auth.Security;

public static class ClaimsPrincipalExtensions
{
    public static bool IsPlatformAdmin(this ClaimsPrincipal? principal) =>
        principal?.HasClaim(AuthConstants.Claims.Permission, Permission.Platform.Manage) == true;
}
