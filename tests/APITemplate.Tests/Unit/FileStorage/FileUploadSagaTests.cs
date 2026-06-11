using BuildingBlocks.Infrastructure.EFCore.Persistence.DesignTime;
using ErrorOr;
using FileStorage.Contracts;
using FileStorage.Domain;
using FileStorage.Domain.Sagas;
using FileStorage.Domain.Storage;
using FileStorage.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.FileStorage;

[Trait("Category", "Unit")]
[Trait("Category", "Unit.Component")]
public sealed class FileUploadSagaTests
{
    private readonly Mock<IBlobStore> _store = new();
    private readonly Mock<IBlobStoreFactory> _factory = new();
    private readonly FileStorageOptions _options = new()
    {
        BasePath = "/tmp",
        StagingTtlMinutes = 30,
        BackendKey = "local",
        AllowedExtensions = [".png"],
    };

    public FileUploadSagaTests()
    {
        _factory.Setup(f => f.Get(It.IsAny<string>())).Returns(_store.Object);
    }

    private static FileStorageDbContext CreateDbContext() =>
        new(
            new DbContextOptionsBuilder<FileStorageDbContext>()
                .UseInMemoryDatabase($"saga-{Guid.NewGuid():N}")
                .Options,
            DesignTimeServices.TenantProvider,
            DesignTimeServices.ActorProvider,
            TimeProvider.System,
            DesignTimeServices.AuditableEntityStateManager
        );

    private FileUploadSaga Staged(Guid? tenantId = null) =>
        new()
        {
            Id = "token-1",
            TenantId = tenantId ?? Guid.NewGuid(),
            Sha256 = "deadbeef",
            SizeBytes = 100,
            OriginalFileName = "pic.png",
            StagingPath = "/tmp/staging/abc",
            BackendKey = "local",
            Status = FileUploadStatus.Staged,
            CreatedAtUtc = DateTime.UtcNow,
            CommitDeadlineUtc = DateTime.UtcNow.AddMinutes(30),
        };

    [Fact]
    public void Start_InitialisesStagedStatusAndDeadline()
    {
        BeginUploadCommand cmd = new(
            "token-1",
            Guid.NewGuid(),
            "sha",
            5,
            "pic.png",
            "/tmp/stg",
            "local"
        );

        FileUploadSaga saga = FileUploadSaga.Start(
            cmd,
            Options.Create(_options),
            TimeProvider.System
        );

        saga.Id.ShouldBe("token-1");
        saga.Status.ShouldBe(FileUploadStatus.Staged);
        saga.CommitDeadlineUtc.ShouldBeGreaterThan(saga.CreatedAtUtc);
        (saga.CommitDeadlineUtc - saga.CreatedAtUtc).TotalMinutes.ShouldBe(
            _options.StagingTtlMinutes,
            tolerance: 0.01
        );
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(1440)]
    public void Start_DeadlineRespectsConfiguredTtl(int ttlMinutes)
    {
        _options.StagingTtlMinutes = ttlMinutes;
        BeginUploadCommand cmd = new("t", Guid.NewGuid(), "sha", 5, "f.png", "/tmp/s", "local");

        FileUploadSaga saga = FileUploadSaga.Start(
            cmd,
            Options.Create(_options),
            TimeProvider.System
        );

        (saga.CommitDeadlineUtc - saga.CreatedAtUtc).TotalMinutes.ShouldBe(
            ttlMinutes,
            tolerance: 0.01
        );
    }

    [Fact]
    public async Task Handle_CommitOnStaged_PromotesBlobInsertsRowReturnsReply()
    {
        FileUploadSaga saga = Staged();
        using FileStorageDbContext db = CreateDbContext();

        _store
            .Setup(s =>
                s.PromoteToCommittedAsync(
                    saga.TenantId,
                    saga.Sha256,
                    saga.SizeBytes,
                    saga.StagingPath,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync("/tmp/blobs/abc");

        (ErrorOr<UploadCommittedReply> reply, StoredFileCreatedNotification? notification) =
            await saga.Handle(
                new CommitUploadCommand(saga.TenantId, saga.Id!, "image/png", "desc"),
                _factory.Object,
                db,
                new StoredFileRepository(db),
                NullLogger<FileUploadSaga>.Instance,
                TestContext.Current.CancellationToken
            );

        reply.IsError.ShouldBeFalse();
        reply.Value.ContentType.ShouldBe("image/png");
        reply.Value.SizeBytes.ShouldBe(saga.SizeBytes);
        notification.ShouldNotBeNull();
        notification!.Sha256.ShouldBe(saga.Sha256);
        saga.Status.ShouldBe(FileUploadStatus.Committed);
        saga.StoredFileId.ShouldNotBeNull();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        (
            await db
                .StoredFiles.IgnoreQueryFilters()
                .CountAsync(cancellationToken: TestContext.Current.CancellationToken)
        ).ShouldBe(1);
    }

    [Fact]
    public async Task Handle_CommitOnAlreadyCommitted_ReturnsExistingRowIdempotently()
    {
        FileUploadSaga saga = Staged();
        using FileStorageDbContext db = CreateDbContext();

        StoredFile existing = StoredFile.Create(
            "pic.png",
            saga.Sha256,
            saga.BackendKey,
            "image/png",
            saga.SizeBytes,
            null
        );
        existing.TenantId = saga.TenantId;
        db.StoredFiles.Add(existing);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        saga.Status = FileUploadStatus.Committed;
        saga.StoredFileId = existing.Id;

        (ErrorOr<UploadCommittedReply> reply, StoredFileCreatedNotification? notification) =
            await saga.Handle(
                new CommitUploadCommand(saga.TenantId, saga.Id!, "image/png", null),
                _factory.Object,
                db,
                new StoredFileRepository(db),
                NullLogger<FileUploadSaga>.Instance,
                TestContext.Current.CancellationToken
            );

        reply.IsError.ShouldBeFalse();
        reply.Value.StoredFileId.ShouldBe(existing.Id);
        notification.ShouldBeNull();
        _store.Verify(
            s =>
                s.PromoteToCommittedAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Theory]
    [InlineData(FileUploadStatus.Failed)]
    public async Task Handle_CommitOnTerminalFailedState_ReturnsError(FileUploadStatus status)
    {
        FileUploadSaga saga = Staged();
        saga.Status = status;
        using FileStorageDbContext db = CreateDbContext();

        (ErrorOr<UploadCommittedReply> reply, StoredFileCreatedNotification? notification) =
            await saga.Handle(
                new CommitUploadCommand(saga.TenantId, saga.Id!, "image/png", null),
                _factory.Object,
                db,
                new StoredFileRepository(db),
                NullLogger<FileUploadSaga>.Instance,
                TestContext.Current.CancellationToken
            );

        reply.IsError.ShouldBeTrue();
        reply.FirstError.Code.ShouldBe(ErrorCatalog.Files.CommitAfterTerminalState);
        notification.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_TimeoutOnStaged_DeletesStagingAndMarksFailed()
    {
        FileUploadSaga saga = Staged();

        await saga.Handle(
            new TimeoutUploadCommand(saga.Id!),
            _factory.Object,
            NullLogger<FileUploadSaga>.Instance,
            TestContext.Current.CancellationToken
        );

        saga.Status.ShouldBe(FileUploadStatus.Failed);
        _store.Verify(
            s => s.DeleteStagingAsync(saga.StagingPath, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Handle_TimeoutAfterCommit_IsNoOp()
    {
        FileUploadSaga saga = Staged();
        saga.Status = FileUploadStatus.Committed;

        await saga.Handle(
            new TimeoutUploadCommand(saga.Id!),
            _factory.Object,
            NullLogger<FileUploadSaga>.Instance,
            TestContext.Current.CancellationToken
        );

        saga.Status.ShouldBe(FileUploadStatus.Committed);
        _store.Verify(
            s => s.DeleteStagingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Handle_TimeoutOnAlreadyFailed_IsNoOp()
    {
        FileUploadSaga saga = Staged();
        saga.Status = FileUploadStatus.Failed;

        await saga.Handle(
            new TimeoutUploadCommand(saga.Id!),
            _factory.Object,
            NullLogger<FileUploadSaga>.Instance,
            TestContext.Current.CancellationToken
        );

        saga.Status.ShouldBe(FileUploadStatus.Failed);
        _store.Verify(
            s => s.DeleteStagingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
