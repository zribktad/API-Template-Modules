using Identity.ValueObjects;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

public sealed class EmailValueObjectTests
{
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
        var result = email.Normalize();

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
