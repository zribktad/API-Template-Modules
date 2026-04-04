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
}
