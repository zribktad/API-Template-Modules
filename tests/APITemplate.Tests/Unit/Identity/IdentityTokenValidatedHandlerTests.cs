using APITemplate.Tests.Unit.Helpers;
using Identity.Security;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

public sealed class IdentityTokenValidatedHandlerTests
{
    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("not-a-guid")]
    [InlineData("")]
    public void HasValidTenantClaim_WhenMissingOrInvalid_ReturnsFalse(string? tenantValue)
    {
        IdentityTokenValidatedHandler
            .HasValidTenantClaim(TestClaimsPrincipalFactory.WithOptionalTenantClaim(tenantValue))
            .ShouldBeFalse();
    }

    [Fact]
    public void HasValidTenantClaim_WhenNonEmptyGuid_ReturnsTrue()
    {
        string tenantId = Guid.NewGuid().ToString();

        IdentityTokenValidatedHandler
            .HasValidTenantClaim(TestClaimsPrincipalFactory.WithTenantId(tenantId))
            .ShouldBeTrue();
    }

    [Fact]
    public void HasValidTenantClaim_WhenPrincipalNull_ReturnsFalse()
    {
        IdentityTokenValidatedHandler.HasValidTenantClaim(null).ShouldBeFalse();
    }
}
