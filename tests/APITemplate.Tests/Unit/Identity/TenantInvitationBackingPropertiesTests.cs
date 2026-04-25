using APITemplate.Tests.Unit.Helpers;
using Identity.Directory.Entities;
using Identity.ValueObjects;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

[Trait("Category", "Unit")]
public sealed class TenantInvitationBackingPropertiesTests
{
    private static readonly TimeProvider Time = new FakeTimeProvider(
        new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)
    );

    private static TenantInvitation MakeInvitation(string email) =>
        TenantInvitation.Create(email, tokenHash: "hash", expiryHours: 48, Time);

    [Fact]
    public void Create_SetsDbEmailFromValue()
    {
        TenantInvitation invitation = MakeInvitation("alice@example.com");

        invitation.DbEmail.ShouldBe("alice@example.com");
    }

    [Fact]
    public void Create_SetsDbNormalizedEmailToUppercaseInvariant()
    {
        TenantInvitation invitation = MakeInvitation("alice@example.com");

        invitation.DbNormalizedEmail.ShouldBe("ALICE@EXAMPLE.COM");
    }

    [Fact]
    public void Create_TrimsWhitespaceFromEmail()
    {
        TenantInvitation invitation = MakeInvitation("  alice@example.com  ");

        invitation.DbEmail.ShouldBe("alice@example.com");
        invitation.DbNormalizedEmail.ShouldBe("ALICE@EXAMPLE.COM");
    }

    [Fact]
    public void EmailFacade_GetReturnsNormalizedStringBackedByDbEmail()
    {
        TenantInvitation invitation = MakeInvitation("alice@example.com");

        invitation.Email.Value.ShouldBe(invitation.DbEmail);
        invitation.Email.Normalized.ShouldBe(invitation.DbNormalizedEmail);
    }

    [Fact]
    public void EmailFacade_SetUpdatesBothDbEmailAndDbNormalizedEmail()
    {
        TenantInvitation invitation = MakeInvitation("alice@example.com");

        invitation.Email = new NormalizedString("bob@example.com");

        invitation.DbEmail.ShouldBe("bob@example.com");
        invitation.DbNormalizedEmail.ShouldBe("BOB@EXAMPLE.COM");
    }

    [Theory]
    [InlineData("alice@example.com", "ALICE@EXAMPLE.COM")]
    [InlineData("BOB@EXAMPLE.COM", "BOB@EXAMPLE.COM")]
    [InlineData("Mixed.Case@Domain.COM", "MIXED.CASE@DOMAIN.COM")]
    public void Create_NormalizedEmailIsAlwaysUppercaseInvariant(
        string input,
        string expectedNormalized
    )
    {
        TenantInvitation invitation = MakeInvitation(input);

        invitation.DbNormalizedEmail.ShouldBe(expectedNormalized);
    }
}
