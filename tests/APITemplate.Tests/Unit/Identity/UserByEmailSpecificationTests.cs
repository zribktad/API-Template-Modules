using Identity.Directory.Entities;
using Identity.Directory.Features.User;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

[Trait("Category", "Unit")]
public sealed class UserByEmailSpecificationTests
{
    private static AppUser MakeUser(string email) =>
        AppUser.Create("alice", email, keycloakUserId: null);

    [Theory]
    [InlineData("alice@example.com", "alice@example.com")]
    [InlineData("alice@example.com", "ALICE@EXAMPLE.COM")]
    [InlineData("alice@example.com", "Alice@Example.Com")]
    [InlineData("alice@example.com", "  alice@example.com  ")]
    [InlineData("ALICE@EXAMPLE.COM", "alice@example.com")]
    public void Filter_WhenEmailMatchesCaseInsensitively_ReturnsTrue(
        string storedEmail,
        string inputEmail
    )
    {
        AppUser user = MakeUser(storedEmail);
        Func<AppUser, bool> filter = new UserByEmailSpecification(inputEmail)
            .WhereExpressions.Single()
            .Filter.Compile();

        filter(user).ShouldBeTrue();
    }

    [Fact]
    public void Filter_WhenEmailDoesNotMatch_ReturnsFalse()
    {
        AppUser user = MakeUser("alice@example.com");
        Func<AppUser, bool> filter = new UserByEmailSpecification("bob@example.com")
            .WhereExpressions.Single()
            .Filter.Compile();

        filter(user).ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_WhenEmailIsNullOrWhitespace_ThrowsArgumentException(string? email)
    {
        Should.Throw<ArgumentException>(() => new UserByEmailSpecification(email!));
    }
}
