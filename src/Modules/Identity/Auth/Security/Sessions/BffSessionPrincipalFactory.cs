using System.Security.Claims;
using Identity.Auth.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Identity.Auth.Security.Sessions;

/// <summary>
///     Rebuilds cookie authentication principals and tickets from the persisted server-side BFF
///     session record.
/// </summary>
public sealed class BffSessionPrincipalFactory : IBffSessionPrincipalFactory
{
    private readonly BffOptions _options;
    private readonly TimeProvider _timeProvider;

    public BffSessionPrincipalFactory(IOptions<BffOptions> options, TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public ClaimsPrincipal CreatePrincipal(BffSessionRecord session)
    {
        List<Claim> claims =
        [
            new Claim(ClaimTypes.NameIdentifier, session.UserId),
            new Claim(AuthConstants.Claims.Subject, session.Subject),
        ];

        if (!string.IsNullOrWhiteSpace(session.DisplayName))
            claims.Add(new Claim(ClaimTypes.Name, session.DisplayName));

        if (!string.IsNullOrWhiteSpace(session.Email))
            claims.Add(new Claim(ClaimTypes.Email, session.Email));

        if (!string.IsNullOrWhiteSpace(session.TenantId))
            claims.Add(new Claim(AuthConstants.Claims.TenantId, session.TenantId));

        foreach (string role in session.Roles)
        {
            if (!string.IsNullOrWhiteSpace(role))
                claims.Add(new Claim(ClaimTypes.Role, role));
        }

        ClaimsIdentity identity = new(claims, AuthConstants.BffSchemes.Cookie);
        return new ClaimsPrincipal(identity);
    }

    /// <inheritdoc />
    public AuthenticationTicket CreateTicket(BffSessionRecord session, string authenticationScheme)
    {
        ClaimsPrincipal principal = CreatePrincipal(session);
        AuthenticationProperties properties = new()
        {
            IssuedUtc = session.LastSeenAtUtc,
            ExpiresUtc = GetCookieExpiresAt(session),
        };

        List<AuthenticationToken> tokens =
        [
            new AuthenticationToken
            {
                Name = AuthConstants.CookieTokenNames.AccessToken,
                Value = session.AccessToken,
            },
            new AuthenticationToken
            {
                Name = AuthConstants.CookieTokenNames.RefreshToken,
                Value = session.RefreshToken,
            },
            new AuthenticationToken
            {
                Name = AuthConstants.CookieTokenNames.ExpiresAt,
                Value = session.AccessTokenExpiresAtUtc.ToString("o"),
            },
        ];

        if (!string.IsNullOrWhiteSpace(session.IdToken))
        {
            tokens.Add(
                new AuthenticationToken
                {
                    Name = AuthConstants.CookieTokenNames.IdToken,
                    Value = session.IdToken,
                }
            );
        }

        properties.StoreTokens(tokens);

        return new AuthenticationTicket(principal, properties, authenticationScheme);
    }

    private DateTimeOffset GetCookieExpiresAt(BffSessionRecord session)
    {
        DateTimeOffset idleExpiry = session.LastSeenAtUtc.AddMinutes(
            _options.SessionIdleTimeoutMinutes
        );
        DateTimeOffset absoluteExpiry = session.CreatedAtUtc.AddMinutes(
            _options.SessionAbsoluteLifetimeMinutes
        );

        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (idleExpiry < now)
            return now;

        return idleExpiry <= absoluteExpiry ? idleExpiry : absoluteExpiry;
    }
}
