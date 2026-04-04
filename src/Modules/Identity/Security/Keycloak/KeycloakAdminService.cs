using System.Net;
using Identity.Logging;
using Identity.Options;
using Keycloak.AuthServices.Sdk.Admin;
using Keycloak.AuthServices.Sdk.Admin.Models;
using Keycloak.AuthServices.Sdk.Admin.Requests.Users;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Security.Keycloak;

/// <summary>
///     Keycloak Admin REST API client facade that wraps user lifecycle operations
///     (create, enable/disable, password reset, delete) using the Keycloak SDK.
/// </summary>
public sealed class KeycloakAdminService : IKeycloakAdminService
{
    private readonly ILogger<KeycloakAdminService> _logger;
    private readonly string _realm;
    private readonly IKeycloakUserClient _userClient;

    public KeycloakAdminService(
        IKeycloakUserClient userClient,
        IOptions<KeycloakOptions> keycloakOptions,
        ILogger<KeycloakAdminService> logger
    )
    {
        _userClient = userClient;
        _realm = keycloakOptions.Value.Realm;
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
        response.EnsureSuccessStatusCode();

        string keycloakUserId = ExtractUserIdFromLocation(response);

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
