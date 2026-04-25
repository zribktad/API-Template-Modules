using Identity.Directory.Entities;
using Identity.ValueObjects;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

[Trait("Category", "Unit")]
public sealed class AppUserBackingPropertiesTests
{
    [Fact]
    public void Create_SetsDbEmailFromValue()
    {
        AppUser user = AppUser.Create("alice", "alice@example.com", keycloakUserId: null);

        user.DbEmail.ShouldBe("alice@example.com");
    }

    [Fact]
    public void Create_SetsDbNormalizedEmailToUppercaseInvariant()
    {
        AppUser user = AppUser.Create("alice", "alice@example.com", keycloakUserId: null);

        user.DbNormalizedEmail.ShouldBe("ALICE@EXAMPLE.COM");
    }

    [Fact]
    public void Create_SetsDbUsernameFromValue()
    {
        AppUser user = AppUser.Create("alice", "alice@example.com", keycloakUserId: null);

        user.DbUsername.ShouldBe("alice");
    }

    [Fact]
    public void Create_SetsDbNormalizedUsernameToUppercaseInvariant()
    {
        AppUser user = AppUser.Create("alice", "alice@example.com", keycloakUserId: null);

        user.DbNormalizedUsername.ShouldBe("ALICE");
    }

    [Fact]
    public void Create_TrimsWhitespaceFromEmail()
    {
        AppUser user = AppUser.Create("alice", "  alice@example.com  ", keycloakUserId: null);

        user.DbEmail.ShouldBe("alice@example.com");
        user.DbNormalizedEmail.ShouldBe("ALICE@EXAMPLE.COM");
    }

    [Fact]
    public void Create_TrimsWhitespaceFromUsername()
    {
        AppUser user = AppUser.Create("  alice  ", "alice@example.com", keycloakUserId: null);

        user.DbUsername.ShouldBe("alice");
        user.DbNormalizedUsername.ShouldBe("ALICE");
    }

    [Fact]
    public void EmailFacade_GetReturnsNormalizedStringBackedByDbEmail()
    {
        AppUser user = AppUser.Create("alice", "alice@example.com", keycloakUserId: null);

        user.Email.Value.ShouldBe(user.DbEmail);
        user.Email.Normalized.ShouldBe(user.DbNormalizedEmail);
    }

    [Fact]
    public void UsernameFacade_GetReturnsNormalizedStringBackedByDbUsername()
    {
        AppUser user = AppUser.Create("alice", "alice@example.com", keycloakUserId: null);

        user.Username.Value.ShouldBe(user.DbUsername);
        user.Username.Normalized.ShouldBe(user.DbNormalizedUsername);
    }

    [Fact]
    public void EmailFacade_SetUpdatesBothDbEmailAndDbNormalizedEmail()
    {
        AppUser user = AppUser.Create("alice", "alice@example.com", keycloakUserId: null);

        user.Email = new NormalizedString("bob@example.com");

        user.DbEmail.ShouldBe("bob@example.com");
        user.DbNormalizedEmail.ShouldBe("BOB@EXAMPLE.COM");
    }

    [Fact]
    public void UsernameFacade_SetUpdatesBothDbUsernameAndDbNormalizedUsername()
    {
        AppUser user = AppUser.Create("alice", "alice@example.com", keycloakUserId: null);

        user.Username = new NormalizedString("bob");

        user.DbUsername.ShouldBe("bob");
        user.DbNormalizedUsername.ShouldBe("BOB");
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
        AppUser user = AppUser.Create("alice", input, keycloakUserId: null);

        user.DbNormalizedEmail.ShouldBe(expectedNormalized);
    }

    [Theory]
    [InlineData("alice", "ALICE")]
    [InlineData("Bob.Smith", "BOB.SMITH")]
    [InlineData("UPPERCASE", "UPPERCASE")]
    public void Create_NormalizedUsernameIsAlwaysUppercaseInvariant(
        string input,
        string expectedNormalized
    )
    {
        AppUser user = AppUser.Create(input, "user@example.com", keycloakUserId: null);

        user.DbNormalizedUsername.ShouldBe(expectedNormalized);
    }
}
