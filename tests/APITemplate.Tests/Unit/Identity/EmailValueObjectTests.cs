using APITemplate.Tests.Unit.TestData;
using ErrorOr;
using Identity.ValueObjects;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

public sealed class EmailValueObjectTests
{
    [Theory]
    [MemberData(nameof(EmailTheoryData.InvalidRawInputs), MemberType = typeof(EmailTheoryData))]
    public void Create_WhenInvalid_ReturnsError(string? raw)
    {
        ErrorOr<Email> result = Email.Create(raw!);

        result.IsError.ShouldBeTrue();
    }

    [Theory]
    [MemberData(
        nameof(EmailTheoryData.TrimmingAndNormalizationCases),
        MemberType = typeof(EmailTheoryData)
    )]
    public void Create_WhenValid_TrimsInput(string raw, string expectedValue)
    {
        ErrorOr<Email> result = Email.Create(raw);

        result.IsError.ShouldBeFalse();
        result.Value.Value.ShouldBe(expectedValue);
    }

    [Fact]
    public void Create_WhenValid_CanRoundTripImplicitlyToString()
    {
        ErrorOr<Email> result = Email.Create("person@domain.example");

        ((string)result.Value).ShouldBe("person@domain.example");
    }

    [Fact]
    public void NormalizeRaw_TrimsAndUppercases()
    {
        Email.NormalizeRaw("  a@b.co  ").ShouldBe("A@B.CO");
    }

    [Fact]
    public void Normalize_OnEmail_UppercasesValue()
    {
        Email email = Email.Create("a@b.co").Value;

        email.Normalize().ShouldBe("A@B.CO");
    }

    [Fact]
    public void DefaultEmail_ShouldHaveEmptyValue_InsteadOfNull()
    {
        // Arrange
        Email email = default;

        // Act & Assert
        Assert.NotNull(email.Value);
        Assert.Equal(string.Empty, email.Value);
    }

    [Fact]
    public void Normalize_OnDefaultEmail_ShouldNotThrowNre()
    {
        // Arrange
        Email email = default;

        // Act
        string result = email.Normalize();

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ImplicitConversionToString_OnDefaultEmail_ShouldNotReturnNull()
    {
        // Arrange
        Email email = default;

        // Act
        string value = email;

        // Assert
        Assert.NotNull(value);
        Assert.Equal(string.Empty, value);
    }
}
