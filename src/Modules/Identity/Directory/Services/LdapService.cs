using System.DirectoryServices.Protocols;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using ErrorOr;
using Identity.Directory.Features.User;
using Identity.Directory.Interfaces;
using Identity.Directory.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Directory.Services;

/// <summary>
///     Implementation of LDAP services using System.DirectoryServices.Protocols.
/// </summary>
public sealed class LdapService : ILdapService
{
    private readonly LdapOptions _options;
    private readonly ILogger<LdapService> _logger;

    public LdapService(IOptions<LdapOptions> options, ILogger<LdapService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ErrorOr.ErrorOr<LdapUserResponse>> AuthenticateAsync(
        string username,
        string password,
        CancellationToken ct = default
    )
    {
        try
        {
            using LdapConnection connection = CreateConnection();

            // 1. Find user to get their full DN for binding
            LdapUserResponse? user = await GetUserAsync(username);
            if (user == null)
            {
                _logger.LogWarning(
                    "LDAP authentication failed: User {Username} not found",
                    username
                );
                return Error.Unauthorized(
                    code: "Ldap.UserNotFound",
                    description: "User not found in directory."
                );
            }

            // 2. Try to bind with user credentials
            // For AD with Negotiate (default), UPN (user@domain) is often more reliable than DN.
            string bindUsername = user.DistinguishedName!;
            if (
                !string.IsNullOrEmpty(_options.BindDn)
                && _options.BindDn.Contains('@')
                && !username.Contains('@')
            )
            {
                var domain = _options.BindDn.Split('@')[1];
                bindUsername = $"{username}@{domain}";
            }

            try
            {
                await Task.Run(() =>
                    connection.Bind(new NetworkCredential(bindUsername, password))
                );
            }
            catch (LdapException ex)
                when (ex.ErrorCode == 49 && bindUsername != user.DistinguishedName)
            {
                _logger.LogDebug(
                    "UPN bind failed, falling back to DN bind for {Username}",
                    username
                );
                await Task.Run(() =>
                    connection.Bind(new NetworkCredential(user.DistinguishedName, password))
                );
            }

            return user;
        }
        catch (LdapException ex) when (ex.ErrorCode == 49) // Invalid credentials
        {
            _logger.LogWarning(
                "LDAP authentication failed for user {Username}: Invalid credentials",
                username
            );
            return Error.Unauthorized(
                code: "Ldap.InvalidCredentials",
                description: "Invalid LDAP credentials."
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error during LDAP authentication for user {Username}",
                username
            );
            return Error.Failure(
                code: "Ldap.ConnectionError",
                description: "Could not connect to LDAP server."
            );
        }
    }

    /// <inheritdoc />
    public async Task<LdapUserResponse?> GetUserAsync(
        string username,
        CancellationToken ct = default
    )
    {
        try
        {
            using LdapConnection connection = CreateConnection();

            // Bind with service account if configured
            if (!string.IsNullOrEmpty(_options.BindDn))
            {
                connection.Bind(new NetworkCredential(_options.BindDn, _options.BindPassword));
            }

            string filter = string.Format(_options.UserSearchFilter, username);
            SearchRequest searchRequest = new(
                _options.BaseDn,
                filter,
                SearchScope.Subtree,
                "mail",
                "displayName",
                "cn"
            );

            // Execute search asynchronously
            SearchResponse response = (SearchResponse)
                await Task.Factory.FromAsync(
                    connection.BeginSendRequest,
                    connection.EndSendRequest,
                    searchRequest,
                    PartialResultProcessing.NoPartialResultSupport,
                    null
                );

            if (response.Entries.Count == 0)
            {
                return null;
            }

            SearchResultEntry entry = response.Entries[0];

            Dictionary<string, string[]> attributes = new(StringComparer.OrdinalIgnoreCase);
            foreach (string attrName in entry.Attributes.AttributeNames)
            {
                DirectoryAttribute? attr = entry.Attributes[attrName];
                if (attr != null)
                {
                    try
                    {
                        // Some LDAP attributes are binary (e.g., objectSid, objectGuid) and throw when read as strings.
                        // We filter for string values to populate the response.
                        object[] values = attr.GetValues(typeof(string));
                        attributes[attrName] = values.OfType<string>().ToArray();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(
                            ex,
                            "Skipping binary or incompatible LDAP attribute {AttributeName} for user {Username}",
                            attrName,
                            username
                        );
                    }
                }
            }

            return new LdapUserResponse(
                username,
                attributes.GetValueOrDefault("mail")?.FirstOrDefault(),
                attributes.GetValueOrDefault("displayName")?.FirstOrDefault()
                    ?? attributes.GetValueOrDefault("cn")?.FirstOrDefault(),
                entry.DistinguishedName,
                attributes
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching LDAP directory for user {Username}", username);
            return null;
        }
    }

    private LdapConnection CreateConnection()
    {
        LdapDirectoryIdentifier identifier = new(_options.Host, _options.Port);

        LdapConnection connection = new(identifier);
        connection.SessionOptions.ProtocolVersion = 3;
        // Negotiate is default on Windows and works well with UPNs

        if (_options.UseSsl)
        {
            connection.SessionOptions.SecureSocketLayer = true;
            // Explicitly verify server certificate to prevent MITM.
            connection.SessionOptions.VerifyServerCertificate = (
                LdapConnection conn,
                X509Certificate cert
            ) =>
            {
                if (!_options.ValidateCertificate)
                {
                    _logger.LogWarning(
                        "LDAP Certificate validation skipped by configuration for {Subject}. This is insecure for production.",
                        cert.Subject
                    );
                    return true;
                }

                using (X509Certificate2 cert2 = new(cert))
                {
                    using (X509Chain chain = new())
                    {
                        // Note: We use NoCheck for revocation as internal LDAP servers often lack accessible CRL/OCSP endpoints.
                        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

                        bool isValid = chain.Build(cert2);
                        if (!isValid)
                        {
                            foreach (X509ChainStatus status in chain.ChainStatus)
                            {
                                _logger.LogError(
                                    "LDAP Certificate validation failed for {Subject}: {StatusInformation}",
                                    cert.Subject,
                                    status.StatusInformation
                                );
                            }
                        }

                        return isValid;
                    }
                }
            };
        }

        return connection;
    }
}
