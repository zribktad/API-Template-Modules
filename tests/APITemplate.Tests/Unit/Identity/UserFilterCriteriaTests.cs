using Identity.Directory.Entities;
using Identity.Directory.Features.User;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

[Trait("Category", "Unit")]
public sealed class UserFilterCriteriaTests
{
    private static AppUser MakeUser(string username, string email = "user@example.com") =>
        AppUser.Create(username, email, keycloakUserId: null);

    private static Func<AppUser, bool> CompileFilter(UserFilter filter)
    {
        var spec = new UserFilterSpecification(filter);
        var predicates = spec.WhereExpressions.Select(e => e.Filter.Compile()).ToList();
        return user => predicates.All(p => p(user));
    }

    [Theory]
    [InlineData("alice", "ali")]
    [InlineData("alice", "ALICE")]
    [InlineData("bob.smith", "smith")]
    public void ApplyFilter_SubstringUsername_MatchesContains(
        string storedUsername,
        string searchTerm
    )
    {
        AppUser user = MakeUser(storedUsername);
        Func<AppUser, bool> filter = CompileFilter(new UserFilter(Username: searchTerm));

        filter(user).ShouldBeTrue();
    }

    [Fact]
    public void ApplyFilter_SubstringUsername_DoesNotMatchUnrelated()
    {
        AppUser user = MakeUser("alice");
        Func<AppUser, bool> filter = CompileFilter(new UserFilter(Username: "bob"));

        filter(user).ShouldBeFalse();
    }

    [Theory]
    [InlineData("user@example.com")]
    [InlineData("USER@EXAMPLE.COM")]
    public void ApplyFilter_ExactEmail_MatchesEquality(string searchEmail)
    {
        AppUser user = MakeUser("alice", "user@example.com");
        Func<AppUser, bool> filter = CompileFilter(new UserFilter(Email: searchEmail));

        filter(user).ShouldBeTrue();
    }

    [Fact]
    public void ApplyFilter_ExactEmail_DoesNotMatchDifferentEmail()
    {
        AppUser user = MakeUser("alice", "user@example.com");
        Func<AppUser, bool> filter = CompileFilter(new UserFilter(Email: "other@example.com"));

        filter(user).ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ApplyFilter_EmptyUsername_NoUsernameFilter(string? username)
    {
        AppUser userAlice = MakeUser("alice");
        AppUser userBob = MakeUser("bob");
        Func<AppUser, bool> filter = CompileFilter(new UserFilter(Username: username));

        filter(userAlice).ShouldBeTrue();
        filter(userBob).ShouldBeTrue();
    }
}
