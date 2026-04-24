using APITemplate.Tests.Unit.TestData;
using ErrorOr;
using ProductCatalog.ValueObjects;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.ProductCatalog;

[Trait("Category", "Unit")]
public sealed class PriceValueObjectTests
{
    [Theory]
    [MemberData(
        nameof(PriceTheoryData.InvalidNegativeAmounts),
        MemberType = typeof(PriceTheoryData)
    )]
    public void Create_WhenNegative_ReturnsError(decimal amount)
    {
        ErrorOr<Price> result = Price.Create(amount);

        result.IsError.ShouldBeTrue();
    }

    [Theory]
    [MemberData(
        nameof(PriceTheoryData.ValidNonNegativeAmounts),
        MemberType = typeof(PriceTheoryData)
    )]
    public void Create_WhenNonNegative_ReturnsPrice(decimal amount)
    {
        ErrorOr<Price> result = Price.Create(amount);

        result.IsError.ShouldBeFalse();
        result.Value.Value.ShouldBe(amount);
    }

    [Fact]
    public void Zero_IsZero()
    {
        Price.Zero.Value.ShouldBe(0m);
    }

    [Fact]
    public void FromPersistence_DoesNotRejectNegative()
    {
        Price p = Price.FromPersistence(-5m);

        p.Value.ShouldBe(-5m);
    }
}
