using Identity.Directory.Entities;
using Identity.Directory.Features.User;
using Identity.ValueObjects;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

[Trait("Category", "Unit")]
public sealed class UserByUsernameSpecificationTests
{
    private static AppUser MakeUser(string username) =>
        AppUser.Create(username, "user@example.com", keycloakUserId: null);

    [Theory]
    [InlineData("alice", "alice")]
    [InlineData("alice", "ALICE")]
    [InlineData("alice", "Alice")]
    [InlineData("alice", "  alice  ")]
    [InlineData("Alice", "alice")]
    [InlineData("ALICE", "alice")]
    public void Filter_WhenUsernameMatchesCaseInsensitively_ReturnsTrue(
        string storedUsername,
        string inputUsername
    )
    {
        AppUser user = MakeUser(storedUsername);
        Func<AppUser, bool> filter = new UserByUsernameSpecification(inputUsername)
            .WhereExpressions.Single()
            .Filter.Compile();

        filter(user).ShouldBeTrue();
    }

    [Fact]
    public void Filter_WhenUsernameDoesNotMatch_ReturnsFalse()
    {
        AppUser user = MakeUser("alice");
        Func<AppUser, bool> filter = new UserByUsernameSpecification("bob")
            .WhereExpressions.Single()
            .Filter.Compile();

        filter(user).ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_WhenUsernameIsNullOrWhitespace_ThrowsArgumentException(string? username)
    {
        Should.Throw<ArgumentException>(() => new UserByUsernameSpecification(username!));
    }
}
