using Identity.Auth.Options;
using Identity.Auth.Validation;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

[Trait("Category", "Unit")]
public sealed class KeycloakOptionsValidatorTests
{
    private readonly KeycloakOptionsValidator _sut = new();

    [Fact]
    public void Validate_WhenKeycloakEndpointsNotConfigured_SucceedsEvenIfPasswordVerificationEmpty()
    {
        KeycloakOptions options = new()
        {
            Realm = "",
            AuthServerUrl = "",
            PasswordVerification = new KeycloakPasswordVerificationOptions(),
        };

        ValidateOptionsResult result = _sut.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WhenKeycloakConfiguredAndPasswordVerificationMissing_Fails()
    {
        KeycloakOptions options = new()
        {
            Realm = "demo",
            AuthServerUrl = "https://kc.example.com/",
            PasswordVerification = new KeycloakPasswordVerificationOptions(),
        };

        ValidateOptionsResult result = _sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("passwordVerification");
    }

    [Fact]
    public void Validate_WhenKeycloakConfiguredAndPasswordVerificationSet_Succeeds()
    {
        KeycloakOptions options = new()
        {
            Realm = "demo",
            AuthServerUrl = "https://kc.example.com/",
            PasswordVerification = new KeycloakPasswordVerificationOptions
            {
                ClientId = "verify-client",
                ClientSecret = "verify-secret",
            },
        };

        ValidateOptionsResult result = _sut.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }
}
