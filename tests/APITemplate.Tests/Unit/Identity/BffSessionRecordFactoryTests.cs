using System.Security.Claims;
using Identity.Auth.Security;
using Identity.Auth.Security.Sessions;
using Identity.Auth.Security.Sessions.Lifecycle;
using Microsoft.AspNetCore.Authentication;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

[Trait("Category", "Unit")]
public sealed class BffSessionRecordFactoryTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-04-11T10:00:00Z");

    private static BffSessionRecordFactory CreateSut() => new();

    // -------------------------------------------------------------------------
    // CreateNew — version / timestamps / status
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateNew_SetsVersionToOne()
    {
        AuthenticationTicket ticket = CreateTicket();

        BffSessionRecord result = CreateSut().CreateNew(ticket, Now);

        result.Version.ShouldBe(1L);
    }

    [Fact]
    public void CreateNew_SetsCreatedAtToNow()
    {
        AuthenticationTicket ticket = CreateTicket();

        BffSessionRecord result = CreateSut().CreateNew(ticket, Now);

        result.CreatedAtUtc.ShouldBe(Now);
    }

    [Fact]
    public void CreateNew_SetsLastSeenAtToNow()
    {
        AuthenticationTicket ticket = CreateTicket();

        BffSessionRecord result = CreateSut().CreateNew(ticket, Now);

        result.LastSeenAtUtc.ShouldBe(Now);
    }

    [Fact]
    public void CreateNew_SetsStatusToActive()
    {
        AuthenticationTicket ticket = CreateTicket();

        BffSessionRecord result = CreateSut().CreateNew(ticket, Now);

        result.Status.ShouldBe(BffSessionStatus.Active);
    }

    // -------------------------------------------------------------------------
    // CreateNew — token extraction
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateNew_ExtractsAccessToken()
    {
        AuthenticationTicket ticket = CreateTicket(accessToken: "my-access-token");

        BffSessionRecord result = CreateSut().CreateNew(ticket, Now);

        result.AccessToken.ShouldBe("my-access-token");
    }

    [Fact]
    public void CreateNew_ExtractsRefreshToken()
    {
        AuthenticationTicket ticket = CreateTicket(refreshToken: "my-refresh-token");

        BffSessionRecord result = CreateSut().CreateNew(ticket, Now);

        result.RefreshToken.ShouldBe("my-refresh-token");
    }

    [Fact]
    public void CreateNew_ExtractsIdToken_WhenPresent()
    {
        AuthenticationTicket ticket = CreateTicket(idToken: "my-id-token");

        BffSessionRecord result = CreateSut().CreateNew(ticket, Now);

        result.IdToken.ShouldBe("my-id-token");
    }

    [Fact]
    public void CreateNew_SetsIdTokenNull_WhenMissing()
    {
        AuthenticationTicket ticket = CreateTicket(idToken: null);

        BffSessionRecord result = CreateSut().CreateNew(ticket, Now);

        result.IdToken.ShouldBeNull();
    }

    [Fact]
    public void CreateNew_ParsesAccessTokenExpiry()
    {
        DateTimeOffset expiry = Now.AddMinutes(15);
        AuthenticationTicket ticket = CreateTicket(expiresAt: expiry.ToString("o"));

        BffSessionRecord result = CreateSut().CreateNew(ticket, Now);

        result.AccessTokenExpiresAtUtc.ShouldBe(expiry);
    }

    [Fact]
    public void CreateNew_FallsBackToNow_WhenExpiresAtMissing()
    {
        AuthenticationTicket ticket = CreateTicket(expiresAt: null);

        BffSessionRecord result = CreateSut().CreateNew(ticket, Now);

        result.AccessTokenExpiresAtUtc.ShouldBe(Now);
    }

    [Fact]
    public void CreateNew_FallsBackToNow_WhenExpiresAtMalformed()
    {
        AuthenticationTicket ticket = CreateTicket(expiresAt: "not-a-date");

        BffSessionRecord result = CreateSut().CreateNew(ticket, Now);

        result.AccessTokenExpiresAtUtc.ShouldBe(Now);
    }

    // -------------------------------------------------------------------------
    // CreateNew — claim extraction
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateNew_ExtractsEmail()
    {
        AuthenticationTicket ticket = CreateTicket(email: "user@example.com");

        BffSessionRecord result = CreateSut().CreateNew(ticket, Now);

        result.Email.ShouldBe("user@example.com");
    }

    [Fact]
    public void CreateNew_ExtractsDisplayName()
    {
        AuthenticationTicket ticket = CreateTicket(displayName: "Alice Smith");

        BffSessionRecord result = CreateSut().CreateNew(ticket, Now);

        result.DisplayName.ShouldBe("Alice Smith");
    }

    [Fact]
    public void CreateNew_ExtractsRoles_AndDeduplicates()
    {
        AuthenticationTicket ticket = CreateTicket(roles: ["Admin", "Admin", "User"]);

        BffSessionRecord result = CreateSut().CreateNew(ticket, Now);

        result.Roles.ShouldBe(["Admin", "User"], ignoreOrder: false);
    }

    [Fact]
    public void CreateNew_SubjectFallsBackToNameIdentifier_WhenSubjectClaimMissing()
    {
        // Build ticket without a "sub" claim
        List<Claim> claims = [new(ClaimTypes.NameIdentifier, "fallback-user-id")];
        ClaimsPrincipal principal = new(
            new ClaimsIdentity(claims, AuthConstants.BffSchemes.Cookie)
        );
        AuthenticationProperties properties = new();
        properties.StoreTokens([
            new AuthenticationToken
            {
                Name = AuthConstants.CookieTokenNames.AccessToken,
                Value = "at",
            },
            new AuthenticationToken
            {
                Name = AuthConstants.CookieTokenNames.RefreshToken,
                Value = "rt",
            },
        ]);
        AuthenticationTicket ticket = new(principal, properties, AuthConstants.BffSchemes.Cookie);

        BffSessionRecord result = CreateSut().CreateNew(ticket, Now);

        result.Subject.ShouldBe("fallback-user-id");
    }

    // -------------------------------------------------------------------------
    // CreateUpdated
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateUpdated_IncrementsVersion()
    {
        BffSessionRecord current = CreateSession() with { Version = 5 };
        AuthenticationTicket ticket = CreateTicket();

        BffSessionRecord result = CreateSut()
            .CreateUpdated(current.SessionId, ticket, current, Now);

        result.Version.ShouldBe(6L);
    }

    [Fact]
    public void CreateUpdated_PreservesCreatedAtUtc()
    {
        DateTimeOffset originalCreatedAt = Now.AddDays(-3);
        BffSessionRecord current = CreateSession() with { CreatedAtUtc = originalCreatedAt };
        AuthenticationTicket ticket = CreateTicket();

        BffSessionRecord result = CreateSut()
            .CreateUpdated(current.SessionId, ticket, current, Now);

        result.CreatedAtUtc.ShouldBe(originalCreatedAt);
    }

    [Fact]
    public void CreateUpdated_PreservesRefreshTokenExpiresAtUtc()
    {
        DateTimeOffset rtExpiry = Now.AddDays(30);
        BffSessionRecord current = CreateSession() with { RefreshTokenExpiresAtUtc = rtExpiry };
        AuthenticationTicket ticket = CreateTicket();

        BffSessionRecord result = CreateSut()
            .CreateUpdated(current.SessionId, ticket, current, Now);

        result.RefreshTokenExpiresAtUtc.ShouldBe(rtExpiry);
    }

    [Fact]
    public void CreateUpdated_PreservesLastRefreshedAtUtc()
    {
        DateTimeOffset lastRefreshed = Now.AddMinutes(-30);
        BffSessionRecord current = CreateSession() with { LastRefreshedAtUtc = lastRefreshed };
        AuthenticationTicket ticket = CreateTicket();

        BffSessionRecord result = CreateSut()
            .CreateUpdated(current.SessionId, ticket, current, Now);

        result.LastRefreshedAtUtc.ShouldBe(lastRefreshed);
    }

    [Fact]
    public void CreateUpdated_WhenCurrentIsRevoked_StatusRemainsRevoked()
    {
        BffSessionRecord current = CreateSession() with
        {
            Status = BffSessionStatus.Revoked,
            RevokedAtUtc = Now.AddMinutes(-5),
            RevocationReason = BffSessionRevocationReason.Logout,
        };
        AuthenticationTicket ticket = CreateTicket();

        BffSessionRecord result = CreateSut()
            .CreateUpdated(current.SessionId, ticket, current, Now);

        result.Status.ShouldBe(BffSessionStatus.Revoked);
    }

    [Fact]
    public void CreateUpdated_WhenCurrentIsActive_StatusBecomesActive()
    {
        BffSessionRecord current = CreateSession() with { Status = BffSessionStatus.Active };
        AuthenticationTicket ticket = CreateTicket();

        BffSessionRecord result = CreateSut()
            .CreateUpdated(current.SessionId, ticket, current, Now);

        result.Status.ShouldBe(BffSessionStatus.Active);
    }

    [Fact]
    public void CreateUpdated_PreservesRevokedAtUtcAndRevocationReason()
    {
        DateTimeOffset revokedAt = Now.AddMinutes(-10);
        BffSessionRecord current = CreateSession() with
        {
            Status = BffSessionStatus.Revoked,
            RevokedAtUtc = revokedAt,
            RevocationReason = BffSessionRevocationReason.Logout,
        };
        AuthenticationTicket ticket = CreateTicket();

        BffSessionRecord result = CreateSut()
            .CreateUpdated(current.SessionId, ticket, current, Now);

        result.RevokedAtUtc.ShouldBe(revokedAt);
        result.RevocationReason.ShouldBe(BffSessionRevocationReason.Logout);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static AuthenticationTicket CreateTicket(
        string nameIdentifier = "user-id-1",
        string subject = "sub-1",
        string accessToken = "at",
        string refreshToken = "rt",
        string? idToken = null,
        string? expiresAt = null,
        string? email = null,
        string? displayName = null,
        string[]? roles = null
    )
    {
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, nameIdentifier),
            new(AuthConstants.Claims.Subject, subject),
        ];
        if (email != null)
            claims.Add(new Claim(ClaimTypes.Email, email));
        if (displayName != null)
            claims.Add(new Claim(ClaimTypes.Name, displayName));
        foreach (string role in roles ?? [])
            claims.Add(new Claim(ClaimTypes.Role, role));

        ClaimsPrincipal principal = new(
            new ClaimsIdentity(claims, AuthConstants.BffSchemes.Cookie)
        );
        AuthenticationProperties properties = new();

        List<AuthenticationToken> tokens =
        [
            new AuthenticationToken
            {
                Name = AuthConstants.CookieTokenNames.AccessToken,
                Value = accessToken,
            },
            new AuthenticationToken
            {
                Name = AuthConstants.CookieTokenNames.RefreshToken,
                Value = refreshToken,
            },
        ];
        if (idToken != null)
            tokens.Add(
                new AuthenticationToken
                {
                    Name = AuthConstants.CookieTokenNames.IdToken,
                    Value = idToken,
                }
            );
        if (expiresAt != null)
            tokens.Add(
                new AuthenticationToken
                {
                    Name = AuthConstants.CookieTokenNames.ExpiresAt,
                    Value = expiresAt,
                }
            );

        properties.StoreTokens(tokens);
        return new AuthenticationTicket(principal, properties, AuthConstants.BffSchemes.Cookie);
    }

    private static BffSessionRecord CreateSession() =>
        new()
        {
            SessionId = "session-1",
            UserId = "user-1",
            Subject = "sub-1",
            Provider = BffProviderType.Keycloak,
            AccessToken = "access-1",
            RefreshToken = "refresh-1",
            AccessTokenExpiresAtUtc = Now.AddMinutes(5),
            CreatedAtUtc = Now.AddMinutes(-10),
            LastSeenAtUtc = Now.AddMinutes(-1),
            LastRefreshedAtUtc = Now.AddMinutes(-5),
            Status = BffSessionStatus.Active,
            Version = 1,
        };
}
