using APITemplate.Application.Common.Context;
using APITemplate.Application.Features.User.DTOs;
using APITemplate.Application.Features.User.Specifications;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Enums;
using APITemplate.Infrastructure.Persistence;
using APITemplate.Infrastructure.Persistence.Auditing;
using APITemplate.Infrastructure.Persistence.EntityNormalization;
using APITemplate.Infrastructure.Persistence.SoftDelete;
using APITemplate.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Repositories;

public class UserRepositoryTests : IDisposable
{
    private static readonly Guid TestTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly AppDbContext _dbContext;
    private readonly UserRepository _sut;

    public UserRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = CreateDbContext(options);
        _sut = new UserRepository(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task AddAsync_PersistsUser()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = CreateUser("testuser", "test@example.com");

        await _sut.AddAsync(user, ct);
        await _dbContext.SaveChangesAsync(ct);

        var persisted = await _dbContext.Users.FindAsync([user.Id], ct);
        persisted.ShouldNotBeNull();
        persisted!.Username.ShouldBe("testuser");
        persisted.Email.ShouldBe("test@example.com");
    }

    [Fact]
    public async Task ExistsByEmailAsync_WhenExists_ReturnsTrue()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = CreateUser("user1", "exists@test.com");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(ct);

        var result = await _sut.ExistsByEmailAsync("exists@test.com", ct);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsByEmailAsync_WhenEmailDiffersOnlyByCase_ReturnsTrue()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = CreateUser("user1", "Exists@Test.com");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(ct);

        var result = await _sut.ExistsByEmailAsync("exists@test.com", ct);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsByEmailAsync_WhenNotExists_ReturnsFalse()
    {
        var result = await _sut.ExistsByEmailAsync("nonexistent@test.com", TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task ExistsByUsernameAsync_WhenExists_ReturnsTrue()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = CreateUser("existinguser", "user@test.com");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(ct);

        var result = await _sut.ExistsByUsernameAsync(AppUser.NormalizeUsername("existinguser"), ct);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsByUsernameAsync_WhenNotExists_ReturnsFalse()
    {
        var result = await _sut.ExistsByUsernameAsync("NONEXISTENT", TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_ReturnsUser()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = CreateUser("findme", "find@test.com");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(ct);

        var result = await _sut.GetByIdAsync(user.Id, ct);

        result.ShouldNotBeNull();
        result!.Username.ShouldBe("findme");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ListAsync_WithUsernameFilter_UsesNormalizedUsernameContains()
    {
        var ct = TestContext.Current.CancellationToken;
        _dbContext.Users.AddRange(
            CreateUser("AlphaAdmin", "alpha@example.com"),
            CreateUser("betauser", "beta@example.com"));
        await _dbContext.SaveChangesAsync(ct);

        var result = await _sut.ListAsync(new UserFilterSpecification(new UserFilter(Username: "alpha")), ct);

        result.Select(user => user.Username).ShouldBe(["AlphaAdmin"]);
    }

    [Fact]
    public async Task ListAsync_WithEmailFilter_RequiresExactCaseInsensitiveMatch()
    {
        var ct = TestContext.Current.CancellationToken;
        _dbContext.Users.AddRange(
            CreateUser("exact", "Exact.Match@Test.com"),
            CreateUser("partial", "other@test.com"));
        await _dbContext.SaveChangesAsync(ct);

        var result = await _sut.ListAsync(new UserFilterSpecification(new UserFilter(Email: "exact.match@test.com")), ct);

        result.Select(user => user.Username).ShouldBe(["exact"]);
    }

    private static AppUser CreateUser(string username, string email)
    {
        return new AppUser
        {
            Id = Guid.NewGuid(),
            Username = username,
            NormalizedUsername = AppUser.NormalizeUsername(username),
            Email = email,
            IsActive = true,
            Role = UserRole.User,
            TenantId = TestTenantId,
            Audit = new() { CreatedAtUtc = DateTime.UtcNow }
        };
    }

    private static AppDbContext CreateDbContext(DbContextOptions<AppDbContext> options)
    {
        var stateManager = new AuditableEntityStateManager();

        return new AppDbContext(
            options,
            new TestTenantProvider(),
            new TestActorProvider(),
            TimeProvider.System,
            [],
            new AppUserEntityNormalizationService(),
            stateManager,
            new SoftDeleteProcessor(stateManager));
    }

    private sealed class TestTenantProvider : ITenantProvider
    {
        public Guid TenantId => TestTenantId;
        public bool HasTenant => true;
    }

    private sealed class TestActorProvider : IActorProvider
    {
        public Guid ActorId => Guid.Empty;
    }
}
