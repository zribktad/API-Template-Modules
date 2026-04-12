using System.Net;
using Identity.Auth.Options;
using Identity.Logging;
using Keycloak.AuthServices.Sdk.Admin;
using Keycloak.AuthServices.Sdk.Admin.Models;
using Keycloak.AuthServices.Sdk.Admin.Requests.Users;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Auth.Security.Keycloak;

/// <summary>
///     Keycloak integration: Admin REST API (SDK) for user lifecycle, direct HTTP for logout-all,
///     and resource-owner grant against the token endpoint for password verification.
/// </summary>
public sealed class KeycloakAdminService : IKeycloakAdminService
{
    private readonly ILogger<KeycloakAdminService> _logger;
    private readonly string _realm;
    private readonly string _authServerUrl;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IKeycloakUserClient _userClient;
    private readonly KeycloakOptions _keycloak;
    private readonly KeycloakPasswordVerificationOptions _passwordVerification;

    public KeycloakAdminService(
        IKeycloakUserClient userClient,
        IHttpClientFactory httpClientFactory,
        IOptions<KeycloakOptions> keycloakOptions,
        ILogger<KeycloakAdminService> logger
    )
    {
        _userClient = userClient;
        _httpClientFactory = httpClientFactory;
        _keycloak = keycloakOptions.Value;
        _realm = _keycloak.Realm;
        _authServerUrl = _keycloak.AuthServerUrl;
        _passwordVerification = _keycloak.PasswordVerification;
        _logger = logger;
    }

    /// <summary>
    ///     Creates a new user in Keycloak and, on a best-effort basis, sends them an
    ///     email-verify + set-password action email. Returns the new Keycloak user ID.
    /// </summary>
    public async Task<string> CreateUserAsync(
        string username,
        string email,
        CancellationToken ct = default
    )
    {
        UserRepresentation user = new()
        {
            Username = username,
            Email = email,
            Enabled = true,
            EmailVerified = false,
        };

        using HttpResponseMessage response = await _userClient.CreateUserWithResponseAsync(
            _realm,
            user,
            ct
        );

        string keycloakUserId;

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            // Idempotent retry: user already exists in Keycloak (e.g. handler ran twice due to
            // outbox retry after a DB commit failure). Fetch the existing user ID by username.
            keycloakUserId = await GetExistingUserIdByUsernameAsync(username, ct);
            _logger.KeycloakUserAlreadyExistsResolved(username, keycloakUserId);
            return keycloakUserId;
        }

        response.EnsureSuccessStatusCode();
        keycloakUserId = ExtractUserIdFromLocation(response);

        _logger.KeycloakUserCreated(username, keycloakUserId);

        // Best-effort: if the setup email fails, we still return the created user ID so the
        // caller can persist the local record. The user can be sent a password reset later.
        try
        {
            await _userClient.ExecuteActionsEmailAsync(
                _realm,
                keycloakUserId,
                new ExecuteActionsEmailRequest
                {
                    Actions =
                    [
                        AuthConstants.KeycloakActions.VerifyEmail,
                        AuthConstants.KeycloakActions.UpdatePassword,
                    ],
                },
                ct
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.KeycloakSetupEmailFailed(ex, keycloakUserId);
        }

        return keycloakUserId;
    }

    /// <summary>Triggers a Keycloak update-password action email for the given user.</summary>
    public async Task SendPasswordResetEmailAsync(
        string keycloakUserId,
        CancellationToken ct = default
    )
    {
        await _userClient.ExecuteActionsEmailAsync(
            _realm,
            keycloakUserId,
            new ExecuteActionsEmailRequest
            {
                Actions = [AuthConstants.KeycloakActions.UpdatePassword],
            },
            ct
        );

        _logger.KeycloakPasswordResetEmailSent(keycloakUserId);
    }

    /// <summary>Enables or disables the Keycloak account for the given user.</summary>
    public async Task SetUserEnabledAsync(
        string keycloakUserId,
        bool enabled,
        CancellationToken ct = default
    )
    {
        UserRepresentation patch = new() { Enabled = enabled };
        await _userClient.UpdateUserAsync(_realm, keycloakUserId, patch, ct);

        _logger.KeycloakUserEnabledSet(keycloakUserId, enabled);
    }

    /// <summary>
    ///     Deletes the Keycloak user. A 404 response is treated as success to handle idempotent retries.
    /// </summary>
    public async Task DeleteUserAsync(string keycloakUserId, CancellationToken ct = default)
    {
        try
        {
            await _userClient.DeleteUserAsync(_realm, keycloakUserId, ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Treat 404 as success — the user was already deleted (e.g., retry scenario)
            _logger.KeycloakUserNotFoundOnDelete(keycloakUserId);
            return;
        }

        _logger.KeycloakUserDeleted(keycloakUserId);
    }

    /// <inheritdoc />
    public async Task SetUserPasswordAsync(
        string keycloakUserId,
        string newPassword,
        bool temporary,
        CancellationToken ct = default
    )
    {
        CredentialRepresentation credential = new()
        {
            Type = AuthConstants.KeycloakCredentialTypes.Password,
            Value = newPassword,
            Temporary = temporary,
        };

        await _userClient.ResetPasswordAsync(_realm, keycloakUserId, credential, ct);
        _logger.KeycloakUserPasswordSet(keycloakUserId, temporary);
    }

    /// <inheritdoc />
    public async Task LogoutAllUserSessionsAsync(
        string keycloakUserId,
        CancellationToken ct = default
    )
    {
        HttpClient client = _httpClientFactory.CreateClient(
            AuthConstants.HttpClients.KeycloakAdmin
        );
        string url = KeycloakUrlHelper.BuildAdminUserLogoutUrl(
            _authServerUrl,
            _realm,
            keycloakUserId
        );

        using HttpResponseMessage response = await client.PostAsync(url, null, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.KeycloakUserLogoutAllSessionsUserNotFound(keycloakUserId);
            return;
        }

        response.EnsureSuccessStatusCode();
        _logger.KeycloakUserAllSessionsLoggedOut(keycloakUserId);
    }

    /// <inheritdoc />
    public async Task<bool> ValidateCredentialsAsync(
        string username,
        string password,
        CancellationToken ct = default
    )
    {
        if (
            string.IsNullOrWhiteSpace(_passwordVerification.ClientId)
            || string.IsNullOrWhiteSpace(_passwordVerification.ClientSecret)
        )
            return false;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return false;

        string tokenEndpoint = KeycloakUrlHelper.BuildTokenEndpoint(
            _keycloak.AuthServerUrl,
            _keycloak.Realm
        );

        using FormUrlEncodedContent content = new([
            new KeyValuePair<string, string>(
                AuthConstants.OAuth2FormParameters.GrantType,
                AuthConstants.OAuth2GrantTypes.Password
            ),
            new(AuthConstants.OAuth2FormParameters.ClientId, _passwordVerification.ClientId),
            new(
                AuthConstants.OAuth2FormParameters.ClientSecret,
                _passwordVerification.ClientSecret
            ),
            new(AuthConstants.OAuth2FormParameters.Username, username),
            new(AuthConstants.OAuth2FormParameters.Password, password),
        ]);

        HttpClient client = _httpClientFactory.CreateClient(
            AuthConstants.HttpClients.KeycloakToken
        );
        using HttpResponseMessage response = await client.PostAsync(tokenEndpoint, content, ct);
        return response.IsSuccessStatusCode;
    }

    private async Task<string> GetExistingUserIdByUsernameAsync(
        string username,
        CancellationToken ct
    )
    {
        IEnumerable<UserRepresentation> users = await _userClient.GetUsersAsync(
            _realm,
            new GetUsersRequestParameters
            {
                Username = username,
                Exact = true,
                Max = 1,
            },
            ct
        );

        UserRepresentation? existing = users.FirstOrDefault();

        return existing?.Id
            ?? throw new InvalidOperationException(
                $"Keycloak returned 409 Conflict for username '{username}' but the user could not be found by username lookup."
            );
    }

    private static string ExtractUserIdFromLocation(HttpResponseMessage response)
    {
        Uri location =
            response.Headers.Location
            ?? throw new InvalidOperationException(
                "Keycloak CreateUser response did not include a Location header."
            );

        // Location is: {base}/admin/realms/{realm}/users/{id}
        string userId = location.Segments[^1].TrimEnd('/');

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException(
                $"Could not extract user ID from Keycloak Location header: {location}"
            );
        }

        return userId;
    }
}
