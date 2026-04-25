using System.Security.Claims;
using Identity.Auth.Options;
using Identity.Auth.Security;
using Identity.Auth.Security.Sessions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

[Trait("Category", "Unit")]
public sealed class BffSessionPrincipalFactoryTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-04-11T10:00:00Z");

    // -------------------------------------------------------------------------
    // CreatePrincipal — mandatory claims
    // -------------------------------------------------------------------------

    [Fact]
    public void CreatePrincipal_AlwaysIncludesNameIdentifier()
    {
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession() with
        {
            UserId = "user-42",
        };

        ClaimsPrincipal principal = CreateSut().CreatePrincipal(session);

        principal.FindFirstValue(ClaimTypes.NameIdentifier).ShouldBe("user-42");
    }

    [Fact]
    public void CreatePrincipal_AlwaysIncludesSubject()
    {
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession() with
        {
            Subject = "sub-99",
        };

        ClaimsPrincipal principal = CreateSut().CreatePrincipal(session);

        principal.FindFirstValue(AuthConstants.Claims.Subject).ShouldBe("sub-99");
    }

    // -------------------------------------------------------------------------
    // CreatePrincipal — optional string claims
    // -------------------------------------------------------------------------

    [Fact]
    public void CreatePrincipal_IncludesDisplayName_WhenPresent()
    {
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession() with
        {
            DisplayName = "Alice Smith",
        };

        ClaimsPrincipal principal = CreateSut().CreatePrincipal(session);

        principal.FindFirstValue(ClaimTypes.Name).ShouldBe("Alice Smith");
    }

    [Fact]
    public void CreatePrincipal_OmitsDisplayName_WhenEmpty()
    {
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession() with
        {
            DisplayName = "",
        };

        ClaimsPrincipal principal = CreateSut().CreatePrincipal(session);

        principal.FindFirstValue(ClaimTypes.Name).ShouldBeNull();
    }

    [Fact]
    public void CreatePrincipal_IncludesEmail_WhenPresent()
    {
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession() with
        {
            Email = "alice@example.com",
        };

        ClaimsPrincipal principal = CreateSut().CreatePrincipal(session);

        principal.FindFirstValue(ClaimTypes.Email).ShouldBe("alice@example.com");
    }

    [Fact]
    public void CreatePrincipal_OmitsEmail_WhenEmpty()
    {
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession() with
        {
            Email = "   ",
        };

        ClaimsPrincipal principal = CreateSut().CreatePrincipal(session);

        principal.FindFirstValue(ClaimTypes.Email).ShouldBeNull();
    }

    [Fact]
    public void CreatePrincipal_IncludesRoles_WhenPresent()
    {
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession() with
        {
            Roles = ["Admin", "User"],
        };

        ClaimsPrincipal principal = CreateSut().CreatePrincipal(session);

        IEnumerable<string> roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value);
        roles.ShouldBe(["Admin", "User"], ignoreOrder: true);
    }

    [Fact]
    public void CreatePrincipal_FiltersOutEmptyRoles()
    {
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession() with
        {
            Roles = ["Admin", "", "  ", "User"],
        };

        ClaimsPrincipal principal = CreateSut().CreatePrincipal(session);

        IEnumerable<string> roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value);
        roles.ShouldBe(["Admin", "User"], ignoreOrder: true);
    }

    [Fact]
    public void CreatePrincipal_IncludesTenantId_WhenPresent()
    {
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession() with
        {
            TenantId = "tenant-abc",
        };

        ClaimsPrincipal principal = CreateSut().CreatePrincipal(session);

        principal.FindFirstValue(AuthConstants.Claims.TenantId).ShouldBe("tenant-abc");
    }

    [Fact]
    public void CreatePrincipal_OmitsTenantId_WhenEmpty()
    {
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession() with
        {
            TenantId = null,
        };

        ClaimsPrincipal principal = CreateSut().CreatePrincipal(session);

        principal.FindFirstValue(AuthConstants.Claims.TenantId).ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // CreateTicket — token storage
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateTicket_StoresAccessToken()
    {
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession() with
        {
            AccessToken = "stored-access-token",
        };

        AuthenticationTicket ticket = CreateSut()
            .CreateTicket(session, AuthConstants.BffSchemes.Cookie);

        ticket
            .Properties.GetTokenValue(AuthConstants.CookieTokenNames.AccessToken)
            .ShouldBe("stored-access-token");
    }

    [Fact]
    public void CreateTicket_StoresRefreshToken()
    {
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession() with
        {
            RefreshToken = "stored-refresh-token",
        };

        AuthenticationTicket ticket = CreateSut()
            .CreateTicket(session, AuthConstants.BffSchemes.Cookie);

        ticket
            .Properties.GetTokenValue(AuthConstants.CookieTokenNames.RefreshToken)
            .ShouldBe("stored-refresh-token");
    }

    [Fact]
    public void CreateTicket_StoresExpiresAtInIso8601()
    {
        DateTimeOffset expiry = Now.AddMinutes(5);
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession() with
        {
            AccessTokenExpiresAtUtc = expiry,
        };

        AuthenticationTicket ticket = CreateSut()
            .CreateTicket(session, AuthConstants.BffSchemes.Cookie);

        string? storedExpiresAt = ticket.Properties.GetTokenValue(
            AuthConstants.CookieTokenNames.ExpiresAt
        );
        storedExpiresAt.ShouldBe(expiry.ToString("o"));
    }

    [Fact]
    public void CreateTicket_StoresIdToken_WhenPresent()
    {
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession() with
        {
            IdToken = "my-id-token",
        };

        AuthenticationTicket ticket = CreateSut()
            .CreateTicket(session, AuthConstants.BffSchemes.Cookie);

        ticket
            .Properties.GetTokenValue(AuthConstants.CookieTokenNames.IdToken)
            .ShouldBe("my-id-token");
    }

    [Fact]
    public void CreateTicket_OmitsIdToken_WhenEmpty()
    {
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession() with
        {
            IdToken = null,
        };

        AuthenticationTicket ticket = CreateSut()
            .CreateTicket(session, AuthConstants.BffSchemes.Cookie);

        ticket.Properties.GetTokenValue(AuthConstants.CookieTokenNames.IdToken).ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // CreateTicket — properties
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateTicket_SetsIssuedUtcToLastSeenAtUtc()
    {
        DateTimeOffset lastSeen = Now.AddMinutes(-3);
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession() with
        {
            LastSeenAtUtc = lastSeen,
        };

        AuthenticationTicket ticket = CreateSut()
            .CreateTicket(session, AuthConstants.BffSchemes.Cookie);

        ticket.Properties.IssuedUtc.ShouldBe(lastSeen);
    }

    [Fact]
    public void CreateTicket_UsesProvidedScheme()
    {
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession();

        AuthenticationTicket ticket = CreateSut().CreateTicket(session, "CustomScheme");

        ticket.AuthenticationScheme.ShouldBe("CustomScheme");
    }

    // -------------------------------------------------------------------------
    // GetCookieExpiresAt (tested via CreateTicket.ExpiresUtc)
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateTicket_ExpiresAtIdleTimeout_WhenIdleIsBeforeAbsolute()
    {
        // Idle timeout: 30 min, Absolute: 120 min
        // LastSeen: Now-5min → idleExpiry = Now+25min
        // Created:  Now-10min → absoluteExpiry = Now+110min
        // idleExpiry (Now+25) < absoluteExpiry (Now+110) → result = idleExpiry
        BffOptions options = new()
        {
            SessionIdleTimeoutMinutes = 30,
            SessionAbsoluteLifetimeMinutes = 120,
        };
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession() with
        {
            LastSeenAtUtc = Now.AddMinutes(-5),
            CreatedAtUtc = Now.AddMinutes(-10),
        };
        DateTimeOffset expectedExpiry = Now.AddMinutes(-5).AddMinutes(30); // = Now+25

        AuthenticationTicket ticket = CreateSut(Now, options)
            .CreateTicket(session, AuthConstants.BffSchemes.Cookie);

        ticket.Properties.ExpiresUtc.ShouldBe(expectedExpiry);
    }

    [Fact]
    public void CreateTicket_ExpiresAtAbsoluteTimeout_WhenAbsoluteIsBeforeIdle()
    {
        // Idle timeout: 120 min, Absolute: 30 min
        // LastSeen: Now-5min → idleExpiry = Now+115min
        // Created:  Now-10min → absoluteExpiry = Now+20min
        // absoluteExpiry (Now+20) < idleExpiry (Now+115) → result = absoluteExpiry
        BffOptions options = new()
        {
            SessionIdleTimeoutMinutes = 120,
            SessionAbsoluteLifetimeMinutes = 30,
        };
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession() with
        {
            LastSeenAtUtc = Now.AddMinutes(-5),
            CreatedAtUtc = Now.AddMinutes(-10),
        };
        DateTimeOffset expectedExpiry = Now.AddMinutes(-10).AddMinutes(30); // = Now+20

        AuthenticationTicket ticket = CreateSut(Now, options)
            .CreateTicket(session, AuthConstants.BffSchemes.Cookie);

        ticket.Properties.ExpiresUtc.ShouldBe(expectedExpiry);
    }

    [Fact]
    public void CreateTicket_ExpiresAtNow_WhenIdleExpiryIsInPast()
    {
        // Idle timeout: 5 min
        // LastSeen: Now-10min → idleExpiry = Now-5min (in the past)
        // idleExpiry < now → result = now
        BffOptions options = new()
        {
            SessionIdleTimeoutMinutes = 5,
            SessionAbsoluteLifetimeMinutes = 480,
        };
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession() with
        {
            LastSeenAtUtc = Now.AddMinutes(-10),
            CreatedAtUtc = Now.AddDays(-1),
        };

        AuthenticationTicket ticket = CreateSut(Now, options)
            .CreateTicket(session, AuthConstants.BffSchemes.Cookie);

        ticket.Properties.ExpiresUtc.ShouldBe(Now);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static BffSessionPrincipalFactory CreateSut(
        DateTimeOffset? now = null,
        BffOptions? options = null
    ) => new(Options.Create(options ?? new BffOptions()), new FakeTimeProvider(now ?? Now));

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
