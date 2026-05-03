using BuildingBlocks.Infrastructure.EFCore.Persistence.DesignTime;
using FileStorage.Contracts;
using FileStorage.Domain;
using FileStorage.Persistence;
using FileStorage.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.FileStorage;

[Trait("Category", "Unit")]
[Trait("Category", "Unit.Component")]
public sealed class OrphanBlobSweepServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(),
        "api-template-sweep",
        Guid.NewGuid().ToString("N")
    );
    private readonly FileStorageOptions _options;
    private readonly FakeTimeProvider _time = new();

    public OrphanBlobSweepServiceTests()
    {
        _options = new FileStorageOptions
        {
            BasePath = _tempDir,
            StagingTtlMinutes = 30,
            BlobRetentionHours = 24,
            BackendKey = "local",
            AllowedExtensions = [".txt"],
        };
    }

    private FileStorageDbContext CreateDbContext(string name = "sweep-test")
    {
        DbContextOptions<FileStorageDbContext> opts =
            new DbContextOptionsBuilder<FileStorageDbContext>()
                .UseInMemoryDatabase($"{name}-{Guid.NewGuid():N}")
                .Options;
        return new FileStorageDbContext(
            opts,
            DesignTimeServices.TenantProvider,
            DesignTimeServices.ActorProvider,
            _time,
            DesignTimeServices.AuditableEntityStateManager
        );
    }

    private OrphanBlobSweepService CreateSut(FileStorageDbContext dbContext) =>
        new(
            Options.Create(_options),
            dbContext,
            _time,
            NullLogger<OrphanBlobSweepService>.Instance
        );

    [Fact]
    public async Task SweepAsync_NoDirectories_ReturnsZero()
    {
        using FileStorageDbContext db = CreateDbContext();
        OrphanBlobSweepService sut = CreateSut(db);

        OrphanBlobSweepResult result = await sut.SweepAsync(TestContext.Current.CancellationToken);

        result.StagingDeleted.ShouldBe(0);
        result.BlobsDeleted.ShouldBe(0);
    }

    [Fact]
    public async Task SweepAsync_OldStagingFile_IsDeleted()
    {
        string stagingDir = _options.ResolveStagingPath();
        Directory.CreateDirectory(stagingDir);
        string oldFile = Path.Combine(stagingDir, "orphan");
        await File.WriteAllTextAsync(oldFile, "x", TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddHours(-2));
        _time.SetUtcNow(DateTimeOffset.UtcNow);

        using FileStorageDbContext db = CreateDbContext();
        OrphanBlobSweepResult result = await CreateSut(db)
            .SweepAsync(TestContext.Current.CancellationToken);

        result.StagingDeleted.ShouldBe(1);
        File.Exists(oldFile).ShouldBeFalse();
    }

    [Fact]
    public async Task SweepAsync_FreshStagingFile_IsRetained()
    {
        string stagingDir = _options.ResolveStagingPath();
        Directory.CreateDirectory(stagingDir);
        string freshFile = Path.Combine(stagingDir, "still-valid");
        await File.WriteAllTextAsync(freshFile, "x", TestContext.Current.CancellationToken);
        _time.SetUtcNow(DateTimeOffset.UtcNow);

        using FileStorageDbContext db = CreateDbContext();
        OrphanBlobSweepResult result = await CreateSut(db)
            .SweepAsync(TestContext.Current.CancellationToken);

        result.StagingDeleted.ShouldBe(0);
        File.Exists(freshFile).ShouldBeTrue();
    }

    [Theory]
    [InlineData(25)] // just past 24h retention
    [InlineData(48)]
    [InlineData(100)]
    public async Task SweepAsync_OrphanBlobOlderThanRetention_IsDeleted(int ageHours)
    {
        Guid tenant = Guid.NewGuid();
        string sha = new('a', 64);
        string blobPath = SeedBlob(tenant, sha, "data");
        File.SetLastWriteTimeUtc(blobPath, DateTime.UtcNow.AddHours(-ageHours));
        _time.SetUtcNow(DateTimeOffset.UtcNow);

        using FileStorageDbContext db = CreateDbContext();
        // no StoredFile row → blob is orphan

        OrphanBlobSweepResult result = await CreateSut(db)
            .SweepAsync(TestContext.Current.CancellationToken);

        result.BlobsDeleted.ShouldBe(1);
        File.Exists(blobPath).ShouldBeFalse();
    }

    [Fact]
    public async Task SweepAsync_FreshBlob_IsRetainedEvenWithoutRow()
    {
        Guid tenant = Guid.NewGuid();
        string sha = new('b', 64);
        string blobPath = SeedBlob(tenant, sha, "data");
        _time.SetUtcNow(DateTimeOffset.UtcNow);

        using FileStorageDbContext db = CreateDbContext();
        OrphanBlobSweepResult result = await CreateSut(db)
            .SweepAsync(TestContext.Current.CancellationToken);

        result.BlobsDeleted.ShouldBe(0);
        File.Exists(blobPath).ShouldBeTrue();
    }

    [Fact]
    public async Task SweepAsync_OldBlobWithActiveRow_IsRetained()
    {
        Guid tenant = Guid.NewGuid();
        string sha = new('c', 64);
        string blobPath = SeedBlob(tenant, sha, "data");
        File.SetLastWriteTimeUtc(blobPath, DateTime.UtcNow.AddHours(-100));
        _time.SetUtcNow(DateTimeOffset.UtcNow);

        using FileStorageDbContext db = CreateDbContext();
        StoredFile activeRow = StoredFile.Create("f.txt", sha, "local", "text/plain", 4, null);
        activeRow.TenantId = tenant;
        db.StoredFiles.Add(activeRow);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        OrphanBlobSweepResult result = await CreateSut(db)
            .SweepAsync(TestContext.Current.CancellationToken);

        result.BlobsDeleted.ShouldBe(0);
        File.Exists(blobPath).ShouldBeTrue();
    }

    [Fact]
    public async Task SweepAsync_OldBlobWithOnlySoftDeletedRow_IsDeleted()
    {
        Guid tenant = Guid.NewGuid();
        string sha = new('d', 64);
        string blobPath = SeedBlob(tenant, sha, "data");
        File.SetLastWriteTimeUtc(blobPath, DateTime.UtcNow.AddHours(-100));
        _time.SetUtcNow(DateTimeOffset.UtcNow);

        using FileStorageDbContext db = CreateDbContext();
        StoredFile entity = StoredFile.Create("f.txt", sha, "local", "text/plain", 4, null);
        entity.TenantId = tenant;
        entity.IsDeleted = true;
        db.StoredFiles.Add(entity);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        OrphanBlobSweepResult result = await CreateSut(db)
            .SweepAsync(TestContext.Current.CancellationToken);

        result.BlobsDeleted.ShouldBe(1);
        File.Exists(blobPath).ShouldBeFalse();
    }

    [Fact]
    public async Task SweepAsync_MultipleTenants_EachHandledIndependently()
    {
        Guid tenantA = Guid.NewGuid();
        Guid tenantB = Guid.NewGuid();
        string sha = new('e', 64);
        string blobA = SeedBlob(tenantA, sha, "data");
        string blobB = SeedBlob(tenantB, sha, "data");
        File.SetLastWriteTimeUtc(blobA, DateTime.UtcNow.AddHours(-100));
        File.SetLastWriteTimeUtc(blobB, DateTime.UtcNow.AddHours(-100));
        _time.SetUtcNow(DateTimeOffset.UtcNow);

        using FileStorageDbContext db = CreateDbContext();
        // Only tenant A still has an active row
        StoredFile entityA = StoredFile.Create("f.txt", sha, "local", "text/plain", 4, null);
        entityA.TenantId = tenantA;
        db.StoredFiles.Add(entityA);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        OrphanBlobSweepResult result = await CreateSut(db)
            .SweepAsync(TestContext.Current.CancellationToken);

        result.BlobsDeleted.ShouldBe(1);
        File.Exists(blobA).ShouldBeTrue();
        File.Exists(blobB).ShouldBeFalse();
    }

    [Fact]
    public async Task SweepAsync_Idempotent_SecondRunIsNoOp()
    {
        Guid tenant = Guid.NewGuid();
        string sha = new('f', 64);
        SeedBlob(tenant, sha, "data");
        _time.SetUtcNow(DateTimeOffset.UtcNow);

        using FileStorageDbContext db = CreateDbContext();

        OrphanBlobSweepResult first = await CreateSut(db)
            .SweepAsync(TestContext.Current.CancellationToken);
        OrphanBlobSweepResult second = await CreateSut(db)
            .SweepAsync(TestContext.Current.CancellationToken);

        // fresh blob — neither run deletes; both return zero.
        first.BlobsDeleted.ShouldBe(0);
        second.BlobsDeleted.ShouldBe(0);
    }

    private string SeedBlob(Guid tenantId, string sha, string content)
    {
        string prefixDir = Path.Combine(_options.ResolveBlobsPath(), tenantId.ToString(), sha[..2]);
        Directory.CreateDirectory(prefixDir);
        string path = Path.Combine(prefixDir, sha);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;

        public void SetUtcNow(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
