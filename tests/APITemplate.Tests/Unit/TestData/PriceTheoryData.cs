using Xunit;

namespace APITemplate.Tests.Unit.TestData;

/// <summary>Shared inputs for Price parametrized tests.</summary>
public static class PriceTheoryData
{
    public static TheoryData<decimal> InvalidNegativeAmounts =>
        new() { -0.01m, -1m, decimal.MinValue };

    public static TheoryData<decimal> ValidNonNegativeAmounts =>
        new() { 0m, 0.01m, 1m, 999999999.99m };
}
