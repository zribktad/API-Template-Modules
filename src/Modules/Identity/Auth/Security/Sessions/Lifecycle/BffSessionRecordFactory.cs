using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace Identity.Auth.Security.Sessions.Lifecycle;

internal sealed class BffSessionRecordFactory : IBffSessionRecordFactory
{
    public BffSessionRecord CreateNew(AuthenticationTicket ticket, DateTimeOffset now)
    {
        return CreateRecord(BffSessionIds.NewId(), ticket, now, now, version: 1);
    }

    public BffSessionRecord CreateUpdated(
        string sessionId,
        AuthenticationTicket ticket,
        BffSessionRecord currentSession,
        DateTimeOffset now
    )
    {
        return CreateRecord(
            sessionId,
            ticket,
            currentSession.CreatedAtUtc,
            now,
            currentSession.Version + 1,
            currentSession.RefreshTokenExpiresAtUtc,
            currentSession.LastRefreshedAtUtc,
            currentSession.Status == BffSessionStatus.Revoked
                ? currentSession.Status
                : BffSessionStatus.Active,
            currentSession.RevokedAtUtc,
            currentSession.RevocationReason
        );
    }

    private static BffSessionRecord CreateRecord(
        string sessionId,
        AuthenticationTicket ticket,
        DateTimeOffset createdAtUtc,
        DateTimeOffset now,
        long version,
        DateTimeOffset? refreshTokenExpiresAtUtc = null,
        DateTimeOffset? lastRefreshedAtUtc = null,
        BffSessionStatus status = BffSessionStatus.Active,
        DateTimeOffset? revokedAtUtc = null,
        BffSessionRevocationReason? revocationReason = null
    )
    {
        ClaimsPrincipal principal = ticket.Principal;

        string? nameIdentifier = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        string subject =
            principal.FindFirstValue(AuthConstants.Claims.Subject)
            ?? nameIdentifier
            ?? string.Empty;

        string userId = nameIdentifier ?? subject;

        string accessToken =
            ticket.Properties.GetTokenValue(AuthConstants.CookieTokenNames.AccessToken)
            ?? string.Empty;
        string refreshToken =
            ticket.Properties.GetTokenValue(AuthConstants.CookieTokenNames.RefreshToken)
            ?? string.Empty;
        string? idToken = ticket.Properties.GetTokenValue(AuthConstants.CookieTokenNames.IdToken);

        return new BffSessionRecord
        {
            SessionId = sessionId,
            UserId = userId,
            Subject = subject,
            Provider = BffProviderType.Keycloak,
            TenantId = principal.FindFirstValue(AuthConstants.Claims.TenantId),
            Roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).Distinct().ToArray(),
            Email = principal.FindFirstValue(ClaimTypes.Email),
            DisplayName =
                principal.FindFirstValue(ClaimTypes.Name)
                ?? principal.FindFirstValue(AuthConstants.Claims.PreferredUsername),
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            IdToken = idToken,
            AccessTokenExpiresAtUtc = ResolveAccessTokenExpiry(ticket.Properties, now),
            RefreshTokenExpiresAtUtc = refreshTokenExpiresAtUtc,
            CreatedAtUtc = createdAtUtc,
            LastSeenAtUtc = now,
            LastRefreshedAtUtc = lastRefreshedAtUtc ?? now,
            Status = status,
            Version = version,
            RevokedAtUtc = revokedAtUtc,
            RevocationReason = revocationReason,
        };
    }

    private static DateTimeOffset ResolveAccessTokenExpiry(
        AuthenticationProperties properties,
        DateTimeOffset fallbackNow
    )
    {
        string? expiresAt = properties.GetTokenValue(AuthConstants.CookieTokenNames.ExpiresAt);
        if (expiresAt is not null && DateTimeOffset.TryParse(expiresAt, out DateTimeOffset parsed))
            return parsed;

        return fallbackNow;
    }
}
