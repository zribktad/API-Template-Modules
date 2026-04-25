using APITemplate.Tests.Unit.Helpers;
using Identity.Directory.Entities;
using Identity.Directory.Enums;
using Identity.Directory.Features.TenantInvitation.DTOs;
using Identity.Directory.Features.TenantInvitation.Specifications;
using Identity.ValueObjects;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

[Trait("Category", "Unit")]
public sealed class TenantInvitationSpecificationTests
{
    private static readonly DateTimeOffset FixedUtc = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeProvider Time = new FakeTimeProvider(FixedUtc);
    private static readonly string NormalizedAlice = NormalizedString.Normalize(
        "alice@example.com"
    );

    private static TenantInvitation MakePending(string email) =>
        TenantInvitation.Create(email, tokenHash: "hash", expiryHours: 48, Time);

    private static TenantInvitation MakeAccepted(string email)
    {
        TenantInvitation i = MakePending(email);
        i.Accept(Time).IsError.ShouldBeFalse();
        return i;
    }

    private static TenantInvitation MakeRevoked(string email)
    {
        TenantInvitation i = MakePending(email);
        i.Revoke();
        return i;
    }

    // ── AcceptedInvitationByNormalizedEmailSpecification ──────────────────

    [Fact]
    public void AcceptedSpec_WhenEmailMatchesAndAccepted_ReturnsTrue()
    {
        TenantInvitation invitation = MakeAccepted("alice@example.com");
        Func<TenantInvitation, bool> filter = new AcceptedInvitationByNormalizedEmailSpecification(
            NormalizedAlice
        )
            .WhereExpressions.Single()
            .Filter.Compile();

        filter(invitation).ShouldBeTrue();
    }

    [Fact]
    public void AcceptedSpec_WhenEmailMatchesButPending_ReturnsFalse()
    {
        TenantInvitation invitation = MakePending("alice@example.com");
        Func<TenantInvitation, bool> filter = new AcceptedInvitationByNormalizedEmailSpecification(
            NormalizedAlice
        )
            .WhereExpressions.Single()
            .Filter.Compile();

        filter(invitation).ShouldBeFalse();
    }

    [Fact]
    public void AcceptedSpec_WhenEmailDoesNotMatch_ReturnsFalse()
    {
        TenantInvitation invitation = MakeAccepted("bob@example.com");
        Func<TenantInvitation, bool> filter = new AcceptedInvitationByNormalizedEmailSpecification(
            NormalizedAlice
        )
            .WhereExpressions.Single()
            .Filter.Compile();

        filter(invitation).ShouldBeFalse();
    }

    // ── PendingInvitationByNormalizedEmailSpecification ───────────────────

    [Fact]
    public void PendingSpec_WhenEmailMatchesAndPending_ReturnsTrue()
    {
        TenantInvitation invitation = MakePending("alice@example.com");
        Func<TenantInvitation, bool> filter = new PendingInvitationByNormalizedEmailSpecification(
            NormalizedAlice
        )
            .WhereExpressions.Single()
            .Filter.Compile();

        filter(invitation).ShouldBeTrue();
    }

    [Fact]
    public void PendingSpec_WhenEmailMatchesButAccepted_ReturnsFalse()
    {
        TenantInvitation invitation = MakeAccepted("alice@example.com");
        Func<TenantInvitation, bool> filter = new PendingInvitationByNormalizedEmailSpecification(
            NormalizedAlice
        )
            .WhereExpressions.Single()
            .Filter.Compile();

        filter(invitation).ShouldBeFalse();
    }

    [Fact]
    public void PendingSpec_WhenEmailMatchesButRevoked_ReturnsFalse()
    {
        TenantInvitation invitation = MakeRevoked("alice@example.com");
        Func<TenantInvitation, bool> filter = new PendingInvitationByNormalizedEmailSpecification(
            NormalizedAlice
        )
            .WhereExpressions.Single()
            .Filter.Compile();

        filter(invitation).ShouldBeFalse();
    }

    // ── LatestInvitationByNormalizedEmailSpecification ────────────────────

    [Theory]
    [InlineData("Pending")]
    [InlineData("Accepted")]
    [InlineData("Revoked")]
    public void LatestSpec_WhenEmailMatchesRegardlessOfStatus_ReturnsTrue(string statusLabel)
    {
        TenantInvitation invitation = statusLabel switch
        {
            "Pending" => MakePending("alice@example.com"),
            "Accepted" => MakeAccepted("alice@example.com"),
            "Revoked" => MakeRevoked("alice@example.com"),
            _ => throw new ArgumentOutOfRangeException(),
        };
        Func<TenantInvitation, bool> filter = new LatestInvitationByNormalizedEmailSpecification(
            NormalizedAlice
        )
            .WhereExpressions.Single()
            .Filter.Compile();

        filter(invitation).ShouldBeTrue();
    }

    [Fact]
    public void LatestSpec_WhenEmailDoesNotMatch_ReturnsFalse()
    {
        TenantInvitation invitation = MakePending("bob@example.com");
        Func<TenantInvitation, bool> filter = new LatestInvitationByNormalizedEmailSpecification(
            NormalizedAlice
        )
            .WhereExpressions.Single()
            .Filter.Compile();

        filter(invitation).ShouldBeFalse();
    }

    // ── TenantInvitationFilterSpecification (email filter criteria) ───────

    [Theory]
    [InlineData("alice@example.com", "alice")]
    [InlineData("alice@example.com", "ALICE")]
    [InlineData("alice@example.com", "example.com")]
    public void FilterSpec_SubstringEmail_MatchesContains(string storedEmail, string searchTerm)
    {
        TenantInvitation invitation = MakePending(storedEmail);
        Func<TenantInvitation, bool> filter = BuildFilterPredicate(
            new TenantInvitationFilter(Email: searchTerm)
        );

        filter(invitation).ShouldBeTrue();
    }

    [Fact]
    public void FilterSpec_SubstringEmail_DoesNotMatchUnrelated()
    {
        TenantInvitation invitation = MakePending("alice@example.com");
        Func<TenantInvitation, bool> filter = BuildFilterPredicate(
            new TenantInvitationFilter(Email: "bob")
        );

        filter(invitation).ShouldBeFalse();
    }

    [Theory]
    [InlineData(InvitationStatus.Pending, "Pending")]
    [InlineData(InvitationStatus.Accepted, "Accepted")]
    [InlineData(InvitationStatus.Revoked, "Revoked")]
    public void FilterSpec_StatusFilter_MatchesExactStatusOnly(
        InvitationStatus status,
        string label
    )
    {
        TenantInvitation matching = label switch
        {
            "Pending" => MakePending("alice@example.com"),
            "Accepted" => MakeAccepted("alice@example.com"),
            "Revoked" => MakeRevoked("alice@example.com"),
            _ => throw new ArgumentOutOfRangeException(),
        };
        TenantInvitation nonMatching =
            status == InvitationStatus.Pending
                ? MakeAccepted("alice@example.com")
                : MakePending("alice@example.com");

        Func<TenantInvitation, bool> filter = BuildFilterPredicate(
            new TenantInvitationFilter(Status: status)
        );

        filter(matching).ShouldBeTrue();
        filter(nonMatching).ShouldBeFalse();
    }

    [Fact]
    public void FilterSpec_NoFilters_MatchesAll()
    {
        Func<TenantInvitation, bool> filter = BuildFilterPredicate(new TenantInvitationFilter());

        filter(MakePending("alice@example.com")).ShouldBeTrue();
        filter(MakeAccepted("bob@example.com")).ShouldBeTrue();
        filter(MakeRevoked("carol@example.com")).ShouldBeTrue();
    }

    private static Func<TenantInvitation, bool> BuildFilterPredicate(TenantInvitationFilter filter)
    {
        TenantInvitationFilterSpecification spec = new(filter);
        List<Func<TenantInvitation, bool>> predicates = spec
            .WhereExpressions.Select(e => e.Filter.Compile())
            .ToList();
        return item => predicates.All(p => p(item));
    }
}
