using APITemplate.Tests.Unit.Helpers;
using Identity.Directory.Entities;
using Identity.Directory.Enums;
using Identity.Directory.Repositories;
using Identity.Persistence;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Infrastructure.Auditing;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Postgres;

/// <summary>
///     Exercises <see cref="TenantInvitationRepository" /> against real PostgreSQL with Ardalis specifications.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "Integration.Postgres")]
[Trait("Docker", "true")]
public sealed class TenantInvitationRepositoryPostgresTests
    : IClassFixture<SharedPostgresContainer>,
        IAsyncLifetime
{
    private static readonly DateTimeOffset FixedUtc = new(2026, 4, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly SharedPostgresContainer _postgres;
    private string _connectionString = null!;
    private Guid _tenantId;
    private IdentityDbContext _dbContext = null!;
    private TenantInvitationRepository _repository = null!;
    private readonly TimeProvider _time = new FakeTimeProvider(FixedUtc);

    public TenantInvitationRepositoryPostgresTests(SharedPostgresContainer postgres)
    {
        _postgres = postgres;
    }

    public async ValueTask InitializeAsync()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string databaseName = $"tinvrepo_{Guid.NewGuid():N}";
        _connectionString = await IsolatedPostgresDatabase.CreateAndGetConnectionStringAsync(
            _postgres,
            databaseName,
            ct
        );

        _tenantId = Guid.NewGuid();
        _dbContext = CreateDbContext();
        await _dbContext.Database.MigrateAsync(ct);

        Tenant tenant = Tenant.Create(
            _tenantId,
            "t" + _tenantId.ToString("N")[..12],
            "Repo test tenant"
        );
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync(ct);
        _dbContext.ChangeTracker.Clear();

        _repository = new TenantInvitationRepository(_dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    public static TheoryData<InvitationStatus, bool> NonRevokedByTokenHashCases =>
        new()
        {
            { InvitationStatus.Revoked, false },
            { InvitationStatus.Pending, true },
            { InvitationStatus.Accepted, true },
        };

    [Theory]
    [MemberData(nameof(NonRevokedByTokenHashCases))]
    public async Task GetNonRevokedByTokenHashAsync_MatchesSpecification(
        InvitationStatus status,
        bool expectFound
    )
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string tokenHash = $"tok-{Guid.NewGuid():N}";
        string email = $"nr-{status}-{Guid.NewGuid():N}@example.com";

        await PersistInvitationAsync(email, tokenHash, status, ct);

        TenantInvitation? found = await _repository.GetNonRevokedByTokenHashAsync(tokenHash, ct);

        if (expectFound)
        {
            found.ShouldNotBeNull();
            found!.TokenHash.ShouldBe(tokenHash);
            found.Status.ShouldBe(status);
        }
        else
        {
            found.ShouldBeNull();
        }
    }

    public static TheoryData<InvitationStatus, bool> HasPendingInvitationCases =>
        new()
        {
            { InvitationStatus.Pending, true },
            { InvitationStatus.Accepted, false },
            { InvitationStatus.Revoked, false },
        };

    [Theory]
    [MemberData(nameof(HasPendingInvitationCases))]
    public async Task HasPendingInvitationAsync_MatchesSpecification(
        InvitationStatus status,
        bool expectPending
    )
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string email = $"hp-{status}-{Guid.NewGuid():N}@example.com";
        string normalized = email.Trim().ToUpperInvariant();
        string tokenHash = $"th-{Guid.NewGuid():N}";

        await PersistInvitationAsync(email, tokenHash, status, ct);

        bool hasPending = await _repository.HasPendingInvitationAsync(normalized, ct);

        hasPending.ShouldBe(expectPending);
    }

    private async Task PersistInvitationAsync(
        string email,
        string tokenHash,
        InvitationStatus status,
        CancellationToken ct
    )
    {
        TenantInvitation invitation = TenantInvitation.Create(email, tokenHash, 48, _time);
        invitation.TenantId = _tenantId;
        ApplyStatus(invitation, status, _time);

        _dbContext.TenantInvitations.Add(invitation);
        await _dbContext.SaveChangesAsync(ct);
        _dbContext.ChangeTracker.Clear();
    }

    private static void ApplyStatus(
        TenantInvitation invitation,
        InvitationStatus status,
        TimeProvider time
    )
    {
        switch (status)
        {
            case InvitationStatus.Pending:
                break;
            case InvitationStatus.Accepted:
                invitation.Accept(time).IsError.ShouldBeFalse();
                break;
            case InvitationStatus.Revoked:
                invitation.Revoke();
                break;
            case InvitationStatus.Expired:
                throw new InvalidOperationException(
                    "Expired status is not settable via public API in tests."
                );
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }
    }

    private IdentityDbContext CreateDbContext()
    {
        DbContextOptions<IdentityDbContext> options =
            new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(_connectionString).Options;

        return new IdentityDbContext(
            options,
            new IdentityIntegrationTenantProvider(_tenantId),
            new IdentityIntegrationEmptyActorProvider(),
            _time,
            new AuditableEntityStateManager()
        );
    }
}
