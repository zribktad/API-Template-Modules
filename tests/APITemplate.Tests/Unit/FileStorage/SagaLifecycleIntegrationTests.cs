using System.Text;
using BuildingBlocks.Application.Resilience;
using BuildingBlocks.Domain.Ids;
using BuildingBlocks.Domain.Interfaces;
using BuildingBlocks.Domain.Options;
using BuildingBlocks.Infrastructure.EFCore.Persistence.DesignTime;
using ErrorOr;
using FileStorage.Contracts;
using FileStorage.Domain;
using FileStorage.Domain.Sagas;
using FileStorage.Domain.Services;
using FileStorage.Domain.Storage;
using FileStorage.Features.Delete;
using FileStorage.Features.Sweep;
using FileStorage.Persistence;
using FileStorage.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Polly;
using Polly.Registry;
using Polly.Retry;
using SharedKernel.Contracts.Commands.FileStorage;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.FileStorage;

/// <summary>
///     End-to-end saga lifecycle tests against a real <see cref="LocalBlobStore" /> (tempfs) and the
///     in-memory EF Core provider. Exercises the full Staged→Committed and Staged→Failed paths, per-tenant
///     isolation, dedup + refcount delete, and orphan reaper interactions without requiring Wolverine
///     runtime or Postgres.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "Unit.Component")]
public sealed class SagaLifecycleIntegrationTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(),
        "api-template-saga-lifecycle",
        Guid.NewGuid().ToString("N")
    );
    private readonly FileStorageOptions _options;
    private readonly FakeTime _time = new();
    private readonly LocalBlobStore _blobStore;
    private readonly StubBlobStoreFactory _factory;
    private readonly string _dbName = $"saga-{Guid.NewGuid():N}";
    private readonly MutableTenantProvider _tenantProvider = new();

    public SagaLifecycleIntegrationTests()
    {
        _options = new FileStorageOptions
        {
            BasePath = _tempDir,
            MaxFileSizeBytes = 10 * 1024 * 1024,
            AllowedExtensions = [".txt"],
            AllowedContentTypes = ["text/plain"],
            StagingTtlMinutes = 30,
            BlobRetentionHours = 24,
            BackendKey = "local",
        };

        ResiliencePipelineRegistry<string> registry = new();
        registry.TryAddBuilder(
            ResiliencePipelineKeys.FileStorageDelete,
            (b, _) => b.AddRetry(new RetryStrategyOptions { Delay = TimeSpan.Zero })
        );
        _blobStore = new LocalBlobStore(
            Options.Create(_options),
            new FileStorageDeletePipelineProvider(registry),
            new GuidIdGenerator(),
            NullLogger<LocalBlobStore>.Instance
        );
        _factory = new StubBlobStoreFactory(_blobStore);
    }

    private FileStorageDbContext Db() =>
        new(
            new DbContextOptionsBuilder<FileStorageDbContext>()
                .UseInMemoryDatabase(_dbName)
                .Options,
            _tenantProvider,
            DesignTimeServices.ActorProvider,
            _time,
            DesignTimeServices.AuditableEntityStateManager
        );

    // Runs the handler's transactional action inline; the in-memory provider has no real transaction
    // and the advisory lock is a Postgres no-op, so this faithfully exercises the delete logic.
    private static IUnitOfWork<FileStorageDbMarker> InlineUnitOfWork()
    {
        Mock<IUnitOfWork<FileStorageDbMarker>> uow = new();
        uow.Setup(u =>
                u.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TransactionOptions?>()
                )
            )
            .Returns<Func<Task>, CancellationToken, TransactionOptions?>(
                async (action, _, _) => await action()
            );
        return uow.Object;
    }

    private async Task<StagingResult> StageAsync(string content) =>
        (
            await _blobStore.WriteStagingAsync(new MemoryStream(Encoding.UTF8.GetBytes(content)))
        ).Value;

    [Fact]
    public async Task FullLifecycle_StageCommitDownload_RoundTripsIdenticalContent()
    {
        Guid tenant = Guid.NewGuid();
        StagingResult staging = await StageAsync("hello");
        string token = Guid.NewGuid().ToString("N");

        FileUploadSaga saga = FileUploadSaga.Start(
            new BeginUploadCommand(
                token,
                tenant,
                staging.Sha256,
                staging.SizeBytes,
                "greeting.txt",
                staging.StagingPath,
                "local"
            ),
            Options.Create(_options),
            _time
        );

        using FileStorageDbContext db = Db();
        (ErrorOr<UploadCommittedReply> commitReply, StoredFileCreatedNotification? notif) =
            await saga.Handle(
                new CommitUploadCommand(saga.TenantId, token, "text/plain", null),
                _factory,
                db,
                new StoredFileRepository(db),
                NullLogger<FileUploadSaga>.Instance,
                TestContext.Current.CancellationToken
            );
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        commitReply.IsError.ShouldBeFalse();
        notif.ShouldNotBeNull();
        saga.Status.ShouldBe(FileUploadStatus.Committed);

        ErrorOr<Stream> openResult = await _blobStore.OpenReadAsync(
            tenant,
            staging.Sha256,
            TestContext.Current.CancellationToken
        );
        openResult.IsError.ShouldBeFalse();
        await using Stream readback = openResult.Value;
        using StreamReader reader = new(readback);
        (await reader.ReadToEndAsync(TestContext.Current.CancellationToken)).ShouldBe("hello");
    }

    [Fact]
    public async Task FullLifecycle_TimeoutBeforeCommit_DeletesStagingAndMarksFailed()
    {
        StagingResult staging = await StageAsync("doomed");
        string token = Guid.NewGuid().ToString("N");
        FileUploadSaga saga = FileUploadSaga.Start(
            new BeginUploadCommand(
                token,
                Guid.NewGuid(),
                staging.Sha256,
                staging.SizeBytes,
                "doomed.txt",
                staging.StagingPath,
                "local"
            ),
            Options.Create(_options),
            _time
        );
        File.Exists(staging.StagingPath).ShouldBeTrue();

        await saga.Handle(
            new TimeoutUploadCommand(token),
            _factory,
            NullLogger<FileUploadSaga>.Instance,
            TestContext.Current.CancellationToken
        );

        saga.Status.ShouldBe(FileUploadStatus.Failed);
        File.Exists(staging.StagingPath).ShouldBeFalse();
    }

    [Fact]
    public async Task PerTenantIsolation_IdenticalContentTwoTenants_ProducesTwoBlobs()
    {
        Guid tenantA = Guid.NewGuid();
        Guid tenantB = Guid.NewGuid();
        const string content = "shared bytes";

        using FileStorageDbContext db = Db();

        await CommitForTenant(db, tenantA, content, "tA.txt");
        await CommitForTenant(db, tenantB, content, "tB.txt");

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        List<StoredFile> rows = await db
            .StoredFiles.AsNoTracking()
            .IgnoreQueryFilters()
            .ToListAsync(TestContext.Current.CancellationToken);
        rows.Count.ShouldBe(2);
        rows[0].Sha256.ShouldBe(rows[1].Sha256); // same hash
        rows.Select(r => r.TenantId).Distinct().Count().ShouldBe(2);

        string shaHex = rows[0].Sha256;
        string pathA = Path.Combine(
            _options.ResolveBlobsPath(),
            tenantA.ToString(),
            shaHex[..2],
            shaHex
        );
        string pathB = Path.Combine(
            _options.ResolveBlobsPath(),
            tenantB.ToString(),
            shaHex[..2],
            shaHex
        );
        File.Exists(pathA).ShouldBeTrue();
        File.Exists(pathB).ShouldBeTrue();
        pathA.ShouldNotBe(pathB);
    }

    [Fact]
    public async Task IntraTenantDedup_SameContentTwice_OneBlobTwoRows()
    {
        Guid tenant = Guid.NewGuid();
        const string content = "dup me";

        using FileStorageDbContext db = Db();
        await CommitForTenant(db, tenant, content, "a.txt");
        await CommitForTenant(db, tenant, content, "b.txt");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        List<StoredFile> rows = await db
            .StoredFiles.AsNoTracking()
            .IgnoreQueryFilters()
            .ToListAsync(TestContext.Current.CancellationToken);
        rows.Count.ShouldBe(2);
        string sha = rows[0].Sha256;

        string blobPath = Path.Combine(
            _options.ResolveBlobsPath(),
            tenant.ToString(),
            sha[..2],
            sha
        );
        File.Exists(blobPath).ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteFlow_LastReferenceRemoved_MaybeDeleteBlobRemovesBlob()
    {
        Guid tenant = Guid.NewGuid();
        // DeleteFileCommandHandler scopes by the ambient tenant via the global filter.
        _tenantProvider.TenantId = tenant;
        using FileStorageDbContext db = Db();
        StoredFile row = await CommitForTenant(db, tenant, "solo", "s.txt");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        string sha = row.Sha256;
        string blobPath = Path.Combine(
            _options.ResolveBlobsPath(),
            tenant.ToString(),
            sha[..2],
            sha
        );
        File.Exists(blobPath).ShouldBeTrue();

        // Soft delete via handler
        using FileStorageDbContext db2 = Db();
        (ErrorOr<Success> deleteResult, Wolverine.OutgoingMessages outgoing) =
            await DeleteFileCommandHandler.HandleAsync(
                new DeleteFileCommand(row.Id),
                db2,
                _time,
                DesignTimeServices.ActorProvider,
                TestContext.Current.CancellationToken
            );
        deleteResult.IsError.ShouldBeFalse();
        await db2.SaveChangesAsync(TestContext.Current.CancellationToken);

        outgoing.ShouldContain(m => m is MaybeDeleteBlobCommand);
        MaybeDeleteBlobCommand cascade = outgoing.OfType<MaybeDeleteBlobCommand>().Single();

        // Simulate Wolverine dispatching the cascade to MaybeDeleteBlobHandler
        using FileStorageDbContext db3 = Db();
        StoredFileRepository repo = new(db3);
        await MaybeDeleteBlobHandler.HandleAsync(
            cascade,
            repo,
            _factory,
            InlineUnitOfWork(),
            NullLogger<MaybeDeleteBlobHandler>.Instance,
            TestContext.Current.CancellationToken
        );

        File.Exists(blobPath).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteFlow_OtherRefsStillExist_BlobRetained()
    {
        Guid tenant = Guid.NewGuid();
        // DeleteFileCommandHandler scopes by the ambient tenant via the global filter.
        _tenantProvider.TenantId = tenant;
        using FileStorageDbContext db = Db();
        StoredFile row1 = await CommitForTenant(db, tenant, "shared", "a.txt");
        StoredFile row2 = await CommitForTenant(db, tenant, "shared", "b.txt");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        string blobPath = Path.Combine(
            _options.ResolveBlobsPath(),
            tenant.ToString(),
            row1.Sha256[..2],
            row1.Sha256
        );

        using FileStorageDbContext db2 = Db();
        (_, Wolverine.OutgoingMessages outgoing) = await DeleteFileCommandHandler.HandleAsync(
            new DeleteFileCommand(row1.Id),
            db2,
            _time,
            DesignTimeServices.ActorProvider,
            TestContext.Current.CancellationToken
        );
        await db2.SaveChangesAsync(TestContext.Current.CancellationToken);

        using FileStorageDbContext db3 = Db();
        StoredFileRepository repo = new(db3);
        MaybeDeleteBlobCommand cascade = outgoing.OfType<MaybeDeleteBlobCommand>().Single();
        await MaybeDeleteBlobHandler.HandleAsync(
            cascade,
            repo,
            _factory,
            InlineUnitOfWork(),
            NullLogger<MaybeDeleteBlobHandler>.Instance,
            TestContext.Current.CancellationToken
        );

        File.Exists(blobPath).ShouldBeTrue(); // row2 still references it
    }

    [Fact]
    public async Task OrphanReaper_DeletesAgedBlobsLeftByDirectFilesystemOrphan()
    {
        Guid tenant = Guid.NewGuid();
        // Seed an orphan blob that no StoredFile row references
        string sha = new('a', 64);
        string prefix = Path.Combine(_options.ResolveBlobsPath(), tenant.ToString(), sha[..2]);
        Directory.CreateDirectory(prefix);
        string orphanPath = Path.Combine(prefix, sha);
        await File.WriteAllTextAsync(orphanPath, "garbage", TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(orphanPath, DateTime.UtcNow.AddHours(-48));
        _time.SetUtcNow(DateTimeOffset.UtcNow);

        using FileStorageDbContext db = Db();
        OrphanBlobSweepService sweeper = new(
            Options.Create(_options),
            db,
            _time,
            NullLogger<OrphanBlobSweepService>.Instance
        );

        OrphanBlobSweepResult result = await sweeper.SweepAsync(
            TestContext.Current.CancellationToken
        );

        result.BlobsDeleted.ShouldBe(1);
        File.Exists(orphanPath).ShouldBeFalse();
    }

    [Fact]
    public async Task SweepHandler_DelegatesToSweepService()
    {
        using FileStorageDbContext db = Db();
        OrphanBlobSweepService sweeper = new(
            Options.Create(_options),
            db,
            _time,
            NullLogger<OrphanBlobSweepService>.Instance
        );

        await SweepOrphanBlobsHandler.HandleAsync(
            new SweepOrphanBlobsCommand(),
            sweeper,
            TestContext.Current.CancellationToken
        );
        // No-op run — just asserts the wiring compiles and runs without throwing.
    }

    [Fact]
    public async Task IdempotentCommit_TwoCommitsProduceOneRow()
    {
        Guid tenant = Guid.NewGuid();
        StagingResult staging = await StageAsync("once");
        string token = Guid.NewGuid().ToString("N");
        FileUploadSaga saga = FileUploadSaga.Start(
            new BeginUploadCommand(
                token,
                tenant,
                staging.Sha256,
                staging.SizeBytes,
                "once.txt",
                staging.StagingPath,
                "local"
            ),
            Options.Create(_options),
            _time
        );

        using FileStorageDbContext db = Db();
        (ErrorOr<UploadCommittedReply> first, _) = await saga.Handle(
            new CommitUploadCommand(saga.TenantId, token, "text/plain", null),
            _factory,
            db,
            new StoredFileRepository(db),
            NullLogger<FileUploadSaga>.Instance,
            TestContext.Current.CancellationToken
        );
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        first.IsError.ShouldBeFalse();

        // Simulate redelivery
        (ErrorOr<UploadCommittedReply> second, StoredFileCreatedNotification? notif2) =
            await saga.Handle(
                new CommitUploadCommand(saga.TenantId, token, "text/plain", null),
                _factory,
                db,
                new StoredFileRepository(db),
                NullLogger<FileUploadSaga>.Instance,
                TestContext.Current.CancellationToken
            );
        second.IsError.ShouldBeFalse();
        second.Value.StoredFileId.ShouldBe(first.Value.StoredFileId);
        notif2.ShouldBeNull();
        (
            await db
                .StoredFiles.IgnoreQueryFilters()
                .CountAsync(TestContext.Current.CancellationToken)
        ).ShouldBe(1);
    }

    private async Task<StoredFile> CommitForTenant(
        FileStorageDbContext db,
        Guid tenant,
        string content,
        string fileName
    )
    {
        StagingResult staging = await StageAsync(content);
        string token = Guid.NewGuid().ToString("N");
        FileUploadSaga saga = FileUploadSaga.Start(
            new BeginUploadCommand(
                token,
                tenant,
                staging.Sha256,
                staging.SizeBytes,
                fileName,
                staging.StagingPath,
                "local"
            ),
            Options.Create(_options),
            _time
        );
        (ErrorOr<UploadCommittedReply> reply, _) = await saga.Handle(
            new CommitUploadCommand(saga.TenantId, token, "text/plain", null),
            _factory,
            db,
            new StoredFileRepository(db),
            NullLogger<FileUploadSaga>.Instance,
            TestContext.Current.CancellationToken
        );
        reply.IsError.ShouldBeFalse();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return await db
            .StoredFiles.AsNoTracking()
            .IgnoreQueryFilters()
            .FirstAsync(
                f => f.Id == reply.Value.StoredFileId,
                TestContext.Current.CancellationToken
            );
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private sealed class StubBlobStoreFactory : IBlobStoreFactory
    {
        private readonly IBlobStore _store;

        public StubBlobStoreFactory(IBlobStore store) => _store = store;

        public IBlobStore Get(string backendKey) => _store;
    }

    private sealed class FakeTime : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;

        public void SetUtcNow(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
