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

    private static TenantInvitation MakeWithStatus(InvitationStatus status, string email) =>
        status switch
        {
            InvitationStatus.Pending => MakePending(email),
            InvitationStatus.Accepted => MakeAccepted(email),
            InvitationStatus.Revoked => MakeRevoked(email),
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };

    // ── AcceptedInvitationByNormalizedEmailSpecification ──────────────────

    [Fact]
    public void AcceptedSpec_WhenEmailMatchesAndAccepted_ReturnsTrue()
    {
        TenantInvitation invitation = MakeAccepted("alice@example.com");
        Func<TenantInvitation, bool> filter = new AcceptedInvitationByNormalizedEmailSpecification(
            NormalizedAlice
        ).CompileSingleFilter();

        filter(invitation).ShouldBeTrue();
    }

    [Fact]
    public void AcceptedSpec_WhenEmailMatchesButPending_ReturnsFalse()
    {
        TenantInvitation invitation = MakePending("alice@example.com");
        Func<TenantInvitation, bool> filter = new AcceptedInvitationByNormalizedEmailSpecification(
            NormalizedAlice
        ).CompileSingleFilter();

        filter(invitation).ShouldBeFalse();
    }

    [Fact]
    public void AcceptedSpec_WhenEmailDoesNotMatch_ReturnsFalse()
    {
        TenantInvitation invitation = MakeAccepted("bob@example.com");
        Func<TenantInvitation, bool> filter = new AcceptedInvitationByNormalizedEmailSpecification(
            NormalizedAlice
        ).CompileSingleFilter();

        filter(invitation).ShouldBeFalse();
    }

    // ── PendingInvitationByNormalizedEmailSpecification ───────────────────

    [Fact]
    public void PendingSpec_WhenEmailMatchesAndPending_ReturnsTrue()
    {
        TenantInvitation invitation = MakePending("alice@example.com");
        Func<TenantInvitation, bool> filter = new PendingInvitationByNormalizedEmailSpecification(
            NormalizedAlice
        ).CompileSingleFilter();

        filter(invitation).ShouldBeTrue();
    }

    [Fact]
    public void PendingSpec_WhenEmailMatchesButAccepted_ReturnsFalse()
    {
        TenantInvitation invitation = MakeAccepted("alice@example.com");
        Func<TenantInvitation, bool> filter = new PendingInvitationByNormalizedEmailSpecification(
            NormalizedAlice
        ).CompileSingleFilter();

        filter(invitation).ShouldBeFalse();
    }

    [Fact]
    public void PendingSpec_WhenEmailMatchesButRevoked_ReturnsFalse()
    {
        TenantInvitation invitation = MakeRevoked("alice@example.com");
        Func<TenantInvitation, bool> filter = new PendingInvitationByNormalizedEmailSpecification(
            NormalizedAlice
        ).CompileSingleFilter();

        filter(invitation).ShouldBeFalse();
    }

    // ── LatestInvitationByNormalizedEmailSpecification ────────────────────

    [Theory]
    [InlineData(InvitationStatus.Pending)]
    [InlineData(InvitationStatus.Accepted)]
    [InlineData(InvitationStatus.Revoked)]
    public void LatestSpec_WhenEmailMatchesRegardlessOfStatus_ReturnsTrue(InvitationStatus status)
    {
        TenantInvitation invitation = MakeWithStatus(status, "alice@example.com");
        Func<TenantInvitation, bool> filter = new LatestInvitationByNormalizedEmailSpecification(
            NormalizedAlice
        ).CompileSingleFilter();

        filter(invitation).ShouldBeTrue();
    }

    [Fact]
    public void LatestSpec_WhenEmailDoesNotMatch_ReturnsFalse()
    {
        TenantInvitation invitation = MakePending("bob@example.com");
        Func<TenantInvitation, bool> filter = new LatestInvitationByNormalizedEmailSpecification(
            NormalizedAlice
        ).CompileSingleFilter();

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
    [InlineData(InvitationStatus.Pending)]
    [InlineData(InvitationStatus.Accepted)]
    [InlineData(InvitationStatus.Revoked)]
    public void FilterSpec_StatusFilter_MatchesExactStatusOnly(InvitationStatus status)
    {
        TenantInvitation matching = MakeWithStatus(status, "alice@example.com");
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
        List<Func<TenantInvitation, bool>> predicates = spec.CompileFilters();
        return item => predicates.All(p => p(item));
    }
}
