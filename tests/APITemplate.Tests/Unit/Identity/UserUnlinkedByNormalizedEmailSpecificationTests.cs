using APITemplate.Tests.Unit.Helpers;
using Identity.Directory.Entities;
using Identity.Directory.Features.User;
using Identity.ValueObjects;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

[Trait("Category", "Unit")]
public sealed class UserUnlinkedByNormalizedEmailSpecificationTests
{
    private static readonly string NormalizedEmail = NormalizedString.Normalize(
        "alice@example.com"
    );

    private static AppUser MakeUser(string email, string? keycloakUserId = null) =>
        AppUser.Create("alice", email, keycloakUserId);

    [Fact]
    public void Filter_WhenEmailMatchesAndNoKeycloakId_ReturnsTrue()
    {
        AppUser user = MakeUser("alice@example.com", keycloakUserId: null);
        Func<AppUser, bool> filter = new UserUnlinkedByNormalizedEmailSpecification(
            NormalizedEmail
        ).CompileSingleFilter();

        filter(user).ShouldBeTrue();
    }

    [Fact]
    public void Filter_WhenEmailMatchesButLinkedToKeycloak_ReturnsFalse()
    {
        AppUser user = MakeUser("alice@example.com", keycloakUserId: "kc-abc123");
        Func<AppUser, bool> filter = new UserUnlinkedByNormalizedEmailSpecification(
            NormalizedEmail
        ).CompileSingleFilter();

        filter(user).ShouldBeFalse();
    }

    [Fact]
    public void Filter_WhenEmailDoesNotMatchAndNoKeycloakId_ReturnsFalse()
    {
        AppUser user = MakeUser("bob@example.com", keycloakUserId: null);
        Func<AppUser, bool> filter = new UserUnlinkedByNormalizedEmailSpecification(
            NormalizedEmail
        ).CompileSingleFilter();

        filter(user).ShouldBeFalse();
    }

    [Fact]
    public void Filter_WhenEmailMatchesCaseInsensitively_ReturnsTrue()
    {
        AppUser user = MakeUser("ALICE@EXAMPLE.COM", keycloakUserId: null);
        Func<AppUser, bool> filter = new UserUnlinkedByNormalizedEmailSpecification(
            NormalizedEmail
        ).CompileSingleFilter();

        filter(user).ShouldBeTrue();
    }
}
