using APITemplate.Tests.Unit.Helpers;
using Identity.Auth.Security;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

public sealed class IdentityTokenValidatedPipelineTests
{
    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("not-a-guid")]
    [InlineData("")]
    public void HasValidTenantClaim_WhenMissingOrInvalid_ReturnsFalse(string? tenantValue)
    {
        IdentityTokenValidatedPipeline
            .HasValidTenantClaim(TestClaimsPrincipalFactory.WithOptionalTenantClaim(tenantValue))
            .ShouldBeFalse();
    }

    [Fact]
    public void HasValidTenantClaim_WhenNonEmptyGuid_ReturnsTrue()
    {
        string tenantId = Guid.NewGuid().ToString();

        IdentityTokenValidatedPipeline
            .HasValidTenantClaim(TestClaimsPrincipalFactory.WithTenantId(tenantId))
            .ShouldBeTrue();
    }

    [Fact]
    public void HasValidTenantClaim_WhenPrincipalNull_ReturnsFalse()
    {
        IdentityTokenValidatedPipeline.HasValidTenantClaim(null).ShouldBeFalse();
    }
}
