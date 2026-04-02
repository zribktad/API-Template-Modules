using APITemplate.Application.Common.Options;
using APITemplate.Application.Common.Security;
using Keycloak.AuthServices.Sdk.Admin;
using Keycloak.AuthServices.Sdk.Admin.Models;
using Keycloak.AuthServices.Sdk.Admin.Requests.Users;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace APITemplate.Infrastructure.Security.Keycloak;

/// <summary>
/// Keycloak Admin REST API client facade that wraps user lifecycle operations
/// (create, enable/disable, password reset, delete) using the Keycloak SDK.
/// </summary>
public sealed class KeycloakAdminService : IKeycloakAdminService
{
    private readonly IKeycloakUserClient _userClient;
    private readonly string _realm;
    private readonly ILogger<KeycloakAdminService> _logger;

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
    /// Creates a new user in Keycloak and, on a best-effort basis, sends them an
    /// email-verify + set-password action email. Returns the new Keycloak user ID.
    /// </summary>
    public async Task<string> CreateUserAsync(
        string username,
        string email,
        CancellationToken ct = default
    )
    {
        var user = new UserRepresentation
        {
            Username = username,
            Email = email,
            Enabled = true,
            EmailVerified = false,
        };

        using var response = await _userClient.CreateUserWithResponseAsync(_realm, user, ct);
        response.EnsureSuccessStatusCode();

        var keycloakUserId = ExtractUserIdFromLocation(response);

        _logger.LogInformation(
            "Created Keycloak user {Username} with id {KeycloakUserId}",
            username,
            keycloakUserId
        );

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
            _logger.LogWarning(
                ex,
                "Failed to send setup email for Keycloak user {KeycloakUserId}. User was created but has no setup email.",
                keycloakUserId
            );
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

        _logger.LogInformation(
            "Sent password reset email to Keycloak user {KeycloakUserId}",
            keycloakUserId
        );
    }

    /// <summary>Enables or disables the Keycloak account for the given user.</summary>
    public async Task SetUserEnabledAsync(
        string keycloakUserId,
        bool enabled,
        CancellationToken ct = default
    )
    {
        var patch = new UserRepresentation { Enabled = enabled };
        await _userClient.UpdateUserAsync(_realm, keycloakUserId, patch, ct);

        _logger.LogInformation(
            "Set Keycloak user {KeycloakUserId} enabled={Enabled}",
            keycloakUserId,
            enabled
        );
    }

    /// <summary>
    /// Deletes the Keycloak user. A 404 response is treated as success to handle idempotent retries.
    /// </summary>
    public async Task DeleteUserAsync(string keycloakUserId, CancellationToken ct = default)
    {
        try
        {
            await _userClient.DeleteUserAsync(_realm, keycloakUserId, ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Treat 404 as success — the user was already deleted (e.g., retry scenario)
            _logger.LogWarning(
                "Keycloak user {KeycloakUserId} was not found during delete — treating as already deleted.",
                keycloakUserId
            );
            return;
        }

        _logger.LogInformation("Deleted Keycloak user {KeycloakUserId}", keycloakUserId);
    }

    private static string ExtractUserIdFromLocation(HttpResponseMessage response)
    {
        var location =
            response.Headers.Location
            ?? throw new InvalidOperationException(
                "Keycloak CreateUser response did not include a Location header."
            );

        // Location is: {base}/admin/realms/{realm}/users/{id}
        var userId = location.Segments[^1].TrimEnd('/');

        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidOperationException(
                $"Could not extract user ID from Keycloak Location header: {location}"
            );

        return userId;
    }
}
