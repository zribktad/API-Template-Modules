using Identity.Auth.Security.Sessions;
using Microsoft.AspNetCore.DataProtection;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

[Trait("Category", "Unit")]
public sealed class BffCsrfTokenServiceTests
{
    private static BffCsrfTokenService CreateSut() => new(new EphemeralDataProtectionProvider());

    // ── CreateToken ───────────────────────────────────────────────────────────

    [Fact]
    public void CreateToken_ReturnsNonEmptyString()
    {
        BffCsrfTokenService sut = CreateSut();

        string token = sut.CreateToken("session-1");

        token.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void CreateToken_ReturnsDifferentTokensForDifferentSessions()
    {
        BffCsrfTokenService sut = CreateSut();

        string tokenA = sut.CreateToken("session-a");
        string tokenB = sut.CreateToken("session-b");

        tokenA.ShouldNotBe(tokenB);
    }

    [Fact]
    public void CreateToken_RoundTripsWithIsValid_ForSameSession()
    {
        // Data Protection uses random nonces, so tokens for the same session differ per call.
        // We verify the round-trip contract via IsValid rather than token equality.
        BffCsrfTokenService sut = CreateSut();
        const string sessionId = "session-roundtrip";

        string token = sut.CreateToken(sessionId);

        sut.IsValid(sessionId, token).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateToken_ThrowsArgumentException_WhenSessionIdNullOrWhitespace(string? sessionId)
    {
        BffCsrfTokenService sut = CreateSut();

        Should.Throw<ArgumentException>(() => sut.CreateToken(sessionId!));
    }

    // ── IsValid ───────────────────────────────────────────────────────────────

    [Fact]
    public void IsValid_ReturnsFalse_WhenSessionIdNull()
    {
        BffCsrfTokenService sut = CreateSut();
        string token = sut.CreateToken("session-1");

        sut.IsValid(null, token).ShouldBeFalse();
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenSessionIdWhitespace()
    {
        BffCsrfTokenService sut = CreateSut();
        string token = sut.CreateToken("session-1");

        sut.IsValid("   ", token).ShouldBeFalse();
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenHeaderNull()
    {
        BffCsrfTokenService sut = CreateSut();

        sut.IsValid("session-1", null).ShouldBeFalse();
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenHeaderEmpty()
    {
        BffCsrfTokenService sut = CreateSut();

        sut.IsValid("session-1", string.Empty).ShouldBeFalse();
    }

    [Fact]
    public void IsValid_ReturnsTrue_ForValidRoundTrip()
    {
        BffCsrfTokenService sut = CreateSut();
        const string sessionId = "session-valid";
        string token = sut.CreateToken(sessionId);

        sut.IsValid(sessionId, token).ShouldBeTrue();
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenHeaderIsMalformedBase64()
    {
        BffCsrfTokenService sut = CreateSut();

        sut.IsValid("session-1", "not-valid-base64!!!").ShouldBeFalse();
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenTokenIsFromDifferentSession()
    {
        BffCsrfTokenService sut = CreateSut();
        string tokenForA = sut.CreateToken("session-a");

        sut.IsValid("session-b", tokenForA).ShouldBeFalse();
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenTokenIsTamperedWith()
    {
        BffCsrfTokenService sut = CreateSut();
        string token = sut.CreateToken("session-1");

        // Flip the last character to corrupt the protected payload.
        char lastChar = token[^1];
        char replacement = lastChar == 'A' ? 'B' : 'A';
        string tampered = string.Concat(token.AsSpan(0, token.Length - 1), replacement.ToString());

        sut.IsValid("session-1", tampered).ShouldBeFalse();
    }
}
