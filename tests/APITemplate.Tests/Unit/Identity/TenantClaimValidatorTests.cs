using System.Security.Claims;
using Identity.Security;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

public sealed class TenantClaimValidatorTests
{
    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("not-a-guid")]
    [InlineData("")]
    public void HasValidTenantClaim_WhenMissingOrInvalid_ReturnsFalse(string? tenantValue)
    {
        Claim[] claims = tenantValue is null
            ? []
            : [new Claim(AuthConstants.Claims.TenantId, tenantValue)];
        ClaimsPrincipal principal = new(new ClaimsIdentity(claims, authenticationType: "Test"));

        TenantClaimValidator.HasValidTenantClaim(principal).ShouldBeFalse();
    }

    [Fact]
    public void HasValidTenantClaim_WhenNonEmptyGuid_ReturnsTrue()
    {
        string tenantId = Guid.NewGuid().ToString();
        ClaimsPrincipal principal = new(
            new ClaimsIdentity(
                [new Claim(AuthConstants.Claims.TenantId, tenantId)],
                authenticationType: "Test"
            )
        );

        TenantClaimValidator.HasValidTenantClaim(principal).ShouldBeTrue();
    }

    [Fact]
    public void HasValidTenantClaim_WhenPrincipalNull_ReturnsFalse()
    {
        TenantClaimValidator.HasValidTenantClaim(null).ShouldBeFalse();
    }
}
