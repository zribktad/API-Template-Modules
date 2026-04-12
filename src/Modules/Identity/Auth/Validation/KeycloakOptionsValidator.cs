using Identity.Auth.Options;
using Microsoft.Extensions.Options;

namespace Identity.Auth.Validation;

/// <summary>
///     When Keycloak endpoints are configured, the confidential password-verification client must be
///     present so <c>ValidateCredentialsAsync</c> is not silently unusable.
/// </summary>
public sealed class KeycloakOptionsValidator : IValidateOptions<KeycloakOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, KeycloakOptions options)
    {
        bool keycloakEndpointsConfigured =
            !string.IsNullOrWhiteSpace(options.AuthServerUrl)
            && !string.IsNullOrWhiteSpace(options.Realm);

        if (!keycloakEndpointsConfigured)
            return ValidateOptionsResult.Success;

        if (
            string.IsNullOrWhiteSpace(options.PasswordVerification.ClientId)
            || string.IsNullOrWhiteSpace(options.PasswordVerification.ClientSecret)
        )
        {
            return ValidateOptionsResult.Fail(
                "Keycloak:passwordVerification:clientId and Keycloak:passwordVerification:clientSecret are required when Keycloak:auth-server-url and Keycloak:realm are set. "
                    + "Use a confidential client that allows the resource-owner password credentials grant for server-side verification."
            );
        }

        return ValidateOptionsResult.Success;
    }
}
