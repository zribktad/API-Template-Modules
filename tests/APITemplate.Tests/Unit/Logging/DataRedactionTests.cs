using Identity.Auth.Features.Bff.DTOs;
using Identity.Directory.Entities;
using Identity.Directory.Features.User;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Contracts;
using SharedKernel.Infrastructure.Logging;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Logging;

[Trait("Category", "Unit")]
public class DataRedactionTests
{
    [Fact]
    public void PersonalDataAttribute_ShouldMapToPersonalClassification()
    {
        // Arrange & Act
        var attribute = new PersonalDataAttribute();

        // Assert
        attribute.Classification.ShouldBe(LogDataClassifications.Personal);
    }

    [Fact]
    public void SensitiveDataAttribute_ShouldMapToSensitiveClassification()
    {
        // Arrange & Act
        var attribute = new SensitiveDataAttribute();

        // Assert
        attribute.Classification.ShouldBe(LogDataClassifications.Sensitive);
    }

    [Theory]
    [InlineData(typeof(AppUser), nameof(AppUser.Email))]
    [InlineData(typeof(BffUserResponse), nameof(BffUserResponse.Email))]
    [InlineData(typeof(UserResponse), nameof(UserResponse.Email))]
    [InlineData(typeof(CreateUserRequest), nameof(CreateUserRequest.Email))]
    [InlineData(typeof(UpdateUserRequest), nameof(UpdateUserRequest.Email))]
    [InlineData(typeof(EmailMessage), nameof(EmailMessage.To))]
    public void Property_ShouldHavePersonalDataAttribute(Type type, string propertyName)
    {
        // Arrange
        var property = type.GetProperty(propertyName);

        // Act
        var attribute = property
            ?.GetCustomAttributes(typeof(PersonalDataAttribute), false)
            .FirstOrDefault();

        // Assert
        attribute.ShouldNotBeNull(
            $"Property {type.Name}.{propertyName} should have [PersonalData] attribute."
        );
    }

    [Theory]
    [InlineData(typeof(EmailOptions), nameof(EmailOptions.Username))]
    [InlineData(typeof(EmailOptions), nameof(EmailOptions.Password))]
    public void Property_ShouldHaveSensitiveDataAttribute(Type type, string propertyName)
    {
        // Arrange
        var property = type.GetProperty(propertyName);

        // Act
        var attribute = property
            ?.GetCustomAttributes(typeof(SensitiveDataAttribute), false)
            .FirstOrDefault();

        // Assert
        attribute.ShouldNotBeNull(
            $"Property {type.Name}.{propertyName} should have [SensitiveData] attribute."
        );
    }

    [Fact]
    public void ResolveAppUserAccessHandler_DenyAndLog_ParametersShouldHavePersonalDataAttribute()
    {
        // Arrange
        var method = typeof(ResolveAppUserAccessHandler).GetMethod(
            "DenyAndLog",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
        );

        // Act
        var parameter = method?.GetParameters().FirstOrDefault(p => p.Name == "normalizedEmail");
        var attribute = parameter
            ?.GetCustomAttributes(typeof(PersonalDataAttribute), false)
            .FirstOrDefault();

        // Assert
        attribute.ShouldNotBeNull(
            "Parameter normalizedEmail in DenyAndLog should have [PersonalData] attribute."
        );
    }
}
