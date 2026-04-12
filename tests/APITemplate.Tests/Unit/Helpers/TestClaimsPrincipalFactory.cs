using System.Security.Claims;
using Identity.Auth.Security;

namespace APITemplate.Tests.Unit.Helpers;

internal static class TestClaimsPrincipalFactory
{
    internal const string TestAuthenticationType = "Test";

    internal static ClaimsPrincipal WithTenantId(string tenantId) =>
        new(
            new ClaimsIdentity(
                [new Claim(AuthConstants.Claims.TenantId, tenantId)],
                authenticationType: TestAuthenticationType
            )
        );

    internal static ClaimsPrincipal WithOptionalTenantClaim(string? tenantValue)
    {
        Claim[] claims = tenantValue is null
            ? []
            : [new Claim(AuthConstants.Claims.TenantId, tenantValue)];
        return new ClaimsPrincipal(
            new ClaimsIdentity(claims, authenticationType: TestAuthenticationType)
        );
    }
}
