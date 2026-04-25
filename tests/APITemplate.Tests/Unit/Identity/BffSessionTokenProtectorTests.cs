using System.Security.Cryptography;
using Identity.Auth.Security.Sessions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

[Trait("Category", "Unit")]
public sealed class BffSessionTokenProtectorTests
{
    private static BffSessionRecord CreateSession(string? idToken = "id-token-value") =>
        new()
        {
            SessionId = "s1",
            UserId = "u1",
            Subject = "sub1",
            Provider = BffProviderType.Keycloak,
            AccessToken = "raw-access",
            RefreshToken = "raw-refresh",
            IdToken = idToken,
            AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LastSeenAtUtc = DateTimeOffset.UtcNow,
            LastRefreshedAtUtc = DateTimeOffset.UtcNow,
            Status = BffSessionStatus.Active,
            Version = 1,
            Roles = [],
        };

    private static BffSessionTokenProtector CreateSut(
        ILogger<BffSessionTokenProtector>? logger = null
    )
    {
        EphemeralDataProtectionProvider provider = new();
        return new BffSessionTokenProtector(
            provider,
            logger ?? NullLogger<BffSessionTokenProtector>.Instance
        );
    }

    // ── Protect ──────────────────────────────────────────────────────────────

    [Fact]
    public void Protect_EncryptsAccessToken()
    {
        BffSessionTokenProtector sut = CreateSut();
        BffSessionRecord session = CreateSession();

        BffSessionRecord result = sut.Protect(session);

        result.AccessToken.ShouldNotBe(session.AccessToken);
    }

    [Fact]
    public void Protect_EncryptsRefreshToken()
    {
        BffSessionTokenProtector sut = CreateSut();
        BffSessionRecord session = CreateSession();

        BffSessionRecord result = sut.Protect(session);

        result.RefreshToken.ShouldNotBe(session.RefreshToken);
    }

    [Fact]
    public void Protect_EncryptsIdToken_WhenPresent()
    {
        BffSessionTokenProtector sut = CreateSut();
        BffSessionRecord session = CreateSession(idToken: "id-token-value");

        BffSessionRecord result = sut.Protect(session);

        result.IdToken.ShouldNotBeNull();
        result.IdToken.ShouldNotBe(session.IdToken);
    }

    [Fact]
    public void Protect_SetsIdTokenNull_WhenEmpty()
    {
        BffSessionTokenProtector sut = CreateSut();
        BffSessionRecord session = CreateSession(idToken: string.Empty);

        BffSessionRecord result = sut.Protect(session);

        result.IdToken.ShouldBeNull();
    }

    [Fact]
    public void Protect_SetsIdTokenNull_WhenNull()
    {
        BffSessionTokenProtector sut = CreateSut();
        BffSessionRecord session = CreateSession(idToken: null);

        BffSessionRecord result = sut.Protect(session);

        result.IdToken.ShouldBeNull();
    }

    [Fact]
    public void Protect_PreservesNonTokenFields()
    {
        BffSessionTokenProtector sut = CreateSut();
        BffSessionRecord session = CreateSession();

        BffSessionRecord result = sut.Protect(session);

        result.SessionId.ShouldBe(session.SessionId);
        result.UserId.ShouldBe(session.UserId);
        result.Subject.ShouldBe(session.Subject);
        result.Provider.ShouldBe(session.Provider);
        result.Status.ShouldBe(session.Status);
        result.Version.ShouldBe(session.Version);
    }

    // ── Unprotect ─────────────────────────────────────────────────────────────

    [Fact]
    public void Unprotect_DecryptsAccessToken()
    {
        BffSessionTokenProtector sut = CreateSut();
        BffSessionRecord session = CreateSession();
        BffSessionRecord protected_ = sut.Protect(session);

        BffSessionRecord? result = sut.Unprotect(protected_, session.SessionId);

        result.ShouldNotBeNull();
        result.AccessToken.ShouldBe(session.AccessToken);
    }

    [Fact]
    public void Unprotect_DecryptsRefreshToken()
    {
        BffSessionTokenProtector sut = CreateSut();
        BffSessionRecord session = CreateSession();
        BffSessionRecord protected_ = sut.Protect(session);

        BffSessionRecord? result = sut.Unprotect(protected_, session.SessionId);

        result.ShouldNotBeNull();
        result.RefreshToken.ShouldBe(session.RefreshToken);
    }

    [Fact]
    public void Unprotect_DecryptsIdToken_WhenPresent()
    {
        BffSessionTokenProtector sut = CreateSut();
        BffSessionRecord session = CreateSession(idToken: "id-token-value");
        BffSessionRecord protected_ = sut.Protect(session);

        BffSessionRecord? result = sut.Unprotect(protected_, session.SessionId);

        result.ShouldNotBeNull();
        result.IdToken.ShouldBe(session.IdToken);
    }

    [Fact]
    public void Unprotect_PreservesNullIdToken()
    {
        BffSessionTokenProtector sut = CreateSut();
        BffSessionRecord session = CreateSession(idToken: null);
        BffSessionRecord protected_ = sut.Protect(session);

        BffSessionRecord? result = sut.Unprotect(protected_, session.SessionId);

        result.ShouldNotBeNull();
        result.IdToken.ShouldBeNull();
    }

    [Fact]
    public void Unprotect_WhenAccessTokenTampered_ReturnsNull()
    {
        BffSessionTokenProtector sut = CreateSut();
        BffSessionRecord session = CreateSession();
        BffSessionRecord protected_ = sut.Protect(session);

        // Corrupt the access token so decryption fails with CryptographicException.
        BffSessionRecord tampered = protected_ with
        {
            AccessToken = "corrupted-garbage-payload",
        };

        BffSessionRecord? result = sut.Unprotect(tampered, session.SessionId);

        result.ShouldBeNull();
    }

    [Fact]
    public void Unprotect_LogsEventId3048_WhenCryptographicException()
    {
        Mock<ILogger<BffSessionTokenProtector>> logger = new();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        EventId capturedEventId = default;
        logger
            .Setup(l =>
                l.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                )
            )
            .Callback<LogLevel, EventId, object, Exception?, Delegate>(
                (_, eventId, _, _, _) => capturedEventId = eventId
            );

        BffSessionTokenProtector sut = new(new EphemeralDataProtectionProvider(), logger.Object);
        BffSessionRecord session = CreateSession();
        BffSessionRecord protected_ = sut.Protect(session);
        BffSessionRecord tampered = protected_ with { AccessToken = "corrupted-garbage-payload" };

        BffSessionRecord? result = sut.Unprotect(tampered, session.SessionId);

        result.ShouldBeNull();
        capturedEventId.Id.ShouldBe(3048);
    }
}
