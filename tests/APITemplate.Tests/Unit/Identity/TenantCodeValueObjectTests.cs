using ErrorOr;
using Identity.ValueObjects;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

public sealed class TenantCodeValueObjectTests
{
    [Fact]
    public void Create_WhenNull_ReturnsError()
    {
        ErrorOr<TenantCode> result = TenantCode.Create(null!);

        result.IsError.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenEmptyOrWhitespace_ReturnsError(string raw)
    {
        ErrorOr<TenantCode> result = TenantCode.Create(raw);

        result.IsError.ShouldBeTrue();
    }

    [Fact]
    public void Create_WhenLongerThan100_ReturnsError()
    {
        ErrorOr<TenantCode> result = TenantCode.Create(new string('x', 101));

        result.IsError.ShouldBeTrue();
    }

    [Theory]
    [InlineData("acme", "acme")]
    [InlineData("  acme  ", "acme")]
    public void Create_WhenValid_TrimsAndPreservesValue(string raw, string expected)
    {
        ErrorOr<TenantCode> result = TenantCode.Create(raw);

        result.IsError.ShouldBeFalse();
        result.Value.Value.ShouldBe(expected);
    }

    [Fact]
    public void FromPersistence_BypassesValidation()
    {
        TenantCode code = TenantCode.FromPersistence("any");

        code.Value.ShouldBe("any");
    }
}
