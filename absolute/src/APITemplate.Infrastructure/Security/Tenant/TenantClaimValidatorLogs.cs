using APITemplate.Infrastructure.Logging;
using Microsoft.Extensions.Logging;

namespace APITemplate.Infrastructure.Security.Tenant;

/// <summary>
/// Compile-time source-generated logger extension methods for <see cref="TenantClaimValidator"/> diagnostics.
/// </summary>
internal static partial class TenantClaimValidatorLogs
{
    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Warning,
        Message = "[{Scheme}] Token validated but no identity found"
    )]
    public static partial void TokenValidatedNoIdentity(this ILogger logger, string scheme);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Debug,
        Message = "[{Scheme}] Token validated with {ClaimCount} claims: {Claims}"
    )]
    public static partial void TokenValidatedWithClaims(
        this ILogger logger,
        string scheme,
        int claimCount,
        [SensitiveData] string claims
    );

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Information,
        Message = "[{Scheme}] Authenticated user={User}, tenant={TenantId}, roles=[{Roles}]"
    )]
    public static partial void UserAuthenticated(
        this ILogger logger,
        string scheme,
        [PersonalData] string? user,
        [SensitiveData] string? tenantId,
        string roles
    );

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Warning,
        Message = "User provisioning failed during token validation — authentication will continue"
    )]
    public static partial void UserProvisioningFailed(this ILogger logger, Exception exception);
}
