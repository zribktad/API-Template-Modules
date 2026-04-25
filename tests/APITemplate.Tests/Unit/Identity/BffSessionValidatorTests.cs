using Identity.Auth.Options;
using Identity.Auth.Security.Sessions;
using Identity.Auth.Security.Sessions.Lifecycle;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

[Trait("Category", "Unit")]
public sealed class BffSessionValidatorTests
{
    private static readonly DateTimeOffset Now = BffSessionStoreUnitTestHelpers.DefaultSessionEpoch;

    private static BffSessionValidator CreateSut(BffOptions? options = null) =>
        new(Options.Create(options ?? new BffOptions()));

    [Fact]
    public void Validate_WhenActive_ReturnsAccept()
    {
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession();
        BffSessionValidator sut = CreateSut();

        BffSessionValidationResult result = sut.Validate(session, Now.AddMinutes(-1));

        result.Action.ShouldBe(BffSessionValidationAction.Accept);
        result.RevocationReason.ShouldBeNull();
    }

    [Fact]
    public void Validate_WhenRevoked_ReturnsReject()
    {
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession() with
        {
            Status = BffSessionStatus.Revoked,
        };
        BffSessionValidator sut = CreateSut();

        BffSessionValidationResult result = sut.Validate(session, Now);

        result.Action.ShouldBe(BffSessionValidationAction.Reject);
        result.RevocationReason.ShouldBeNull();
    }

    [Fact]
    public void Validate_WhenExpired_ReturnsReject()
    {
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession() with
        {
            Status = BffSessionStatus.Expired,
        };
        BffSessionValidator sut = CreateSut();

        BffSessionValidationResult result = sut.Validate(session, Now);

        result.Action.ShouldBe(BffSessionValidationAction.Reject);
        result.RevocationReason.ShouldBeNull();
    }

    [Fact]
    public void Validate_WhenSessionIdEmpty_ReturnsRevokeSessionCorrupted()
    {
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession() with
        {
            SessionId = string.Empty,
        };
        BffSessionValidator sut = CreateSut();

        BffSessionValidationResult result = sut.Validate(session, Now);

        result.Action.ShouldBe(BffSessionValidationAction.Revoke);
        result.RevocationReason.ShouldBe(BffSessionRevocationReason.SessionCorrupted);
    }

    [Fact]
    public void Validate_WhenUserIdEmpty_ReturnsRevokeSessionCorrupted()
    {
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession() with
        {
            UserId = string.Empty,
        };
        BffSessionValidator sut = CreateSut();

        BffSessionValidationResult result = sut.Validate(session, Now);

        result.Action.ShouldBe(BffSessionValidationAction.Revoke);
        result.RevocationReason.ShouldBe(BffSessionRevocationReason.SessionCorrupted);
    }

    [Fact]
    public void Validate_WhenSubjectEmpty_ReturnsRevokeSessionCorrupted()
    {
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession() with
        {
            Subject = string.Empty,
        };
        BffSessionValidator sut = CreateSut();

        BffSessionValidationResult result = sut.Validate(session, Now);

        result.Action.ShouldBe(BffSessionValidationAction.Revoke);
        result.RevocationReason.ShouldBe(BffSessionRevocationReason.SessionCorrupted);
    }

    [Fact]
    public void Validate_WhenAccessTokenEmpty_ReturnsRevokeSessionCorrupted()
    {
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession() with
        {
            AccessToken = string.Empty,
        };
        BffSessionValidator sut = CreateSut();

        BffSessionValidationResult result = sut.Validate(session, Now);

        result.Action.ShouldBe(BffSessionValidationAction.Revoke);
        result.RevocationReason.ShouldBe(BffSessionRevocationReason.SessionCorrupted);
    }

    [Fact]
    public void Validate_WhenRefreshTokenExpiryNull_ReturnsAccept()
    {
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession() with
        {
            RefreshTokenExpiresAtUtc = null,
        };
        BffSessionValidator sut = CreateSut();

        BffSessionValidationResult result = sut.Validate(session, Now);

        result.Action.ShouldBe(BffSessionValidationAction.Accept);
    }

    [Fact]
    public void Validate_WhenRefreshTokenExpired_ReturnsExpire()
    {
        DateTimeOffset expiry = Now.AddMinutes(-1);
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession() with
        {
            RefreshTokenExpiresAtUtc = expiry,
        };
        BffSessionValidator sut = CreateSut();

        BffSessionValidationResult result = sut.Validate(session, Now);

        result.Action.ShouldBe(BffSessionValidationAction.Expire);
    }

    [Fact]
    public void Validate_WhenRefreshTokenNotYetExpired_ReturnsAccept()
    {
        DateTimeOffset expiry = Now.AddMinutes(10);
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession() with
        {
            RefreshTokenExpiresAtUtc = expiry,
        };
        BffSessionValidator sut = CreateSut();

        BffSessionValidationResult result = sut.Validate(session, Now);

        result.Action.ShouldBe(BffSessionValidationAction.Accept);
    }

    [Fact]
    public void Validate_WhenAbsoluteLifetimeExceeded_ReturnsRevokeAbsoluteLifetimeExceeded()
    {
        BffOptions options = new BffOptions { SessionAbsoluteLifetimeMinutes = 480 };
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession() with
        {
            CreatedAtUtc = Now.AddMinutes(-481),
        };
        BffSessionValidator sut = CreateSut(options);

        BffSessionValidationResult result = sut.Validate(session, Now);

        result.Action.ShouldBe(BffSessionValidationAction.Revoke);
        result.RevocationReason.ShouldBe(BffSessionRevocationReason.AbsoluteLifetimeExceeded);
    }

    [Fact]
    public void Validate_WhenAbsoluteLifetimeExactlyAtBoundary_ReturnsRevoke()
    {
        BffOptions options = new BffOptions { SessionAbsoluteLifetimeMinutes = 480 };
        // createdAt + 480 minutes == Now  →  now >= absoluteExpiry  →  revoke
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession() with
        {
            CreatedAtUtc = Now.AddMinutes(-480),
        };
        BffSessionValidator sut = CreateSut(options);

        BffSessionValidationResult result = sut.Validate(session, Now);

        result.Action.ShouldBe(BffSessionValidationAction.Revoke);
        result.RevocationReason.ShouldBe(BffSessionRevocationReason.AbsoluteLifetimeExceeded);
    }

    [Fact]
    public void Validate_WhenAbsoluteLifetimeNotYetExceeded_ReturnsAccept()
    {
        BffOptions options = new BffOptions { SessionAbsoluteLifetimeMinutes = 480 };
        // createdAt + 480 minutes is one second in the future
        BffSessionRecord session = BffSessionStoreUnitTestHelpers.CreateSampleSession() with
        {
            CreatedAtUtc = Now.AddMinutes(-480).AddSeconds(1),
        };
        BffSessionValidator sut = CreateSut(options);

        BffSessionValidationResult result = sut.Validate(session, Now);

        result.Action.ShouldBe(BffSessionValidationAction.Accept);
    }
}
