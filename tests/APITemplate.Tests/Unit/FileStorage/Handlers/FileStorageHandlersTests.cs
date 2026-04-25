using ErrorOr;
using FileStorage.Contracts;
using FileStorage.Domain;
using FileStorage.Domain.Sagas;
using FileStorage.Domain.Storage;
using FileStorage.Features.Delete;
using FileStorage.Features.Download;
using FileStorage.Features.Staging;
using FileStorage.Features.Upload;
using FileStorage.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel.Infrastructure.Persistence.DesignTime;
using Shouldly;
using Wolverine;
using Xunit;

namespace APITemplate.Tests.Unit.FileStorage.Handlers;

[Trait("Category", "Unit")]
public sealed class FileStorageHandlersTests
{
    private readonly Mock<IMessageBus> _bus = new();
    private readonly Mock<IStoredFileRepository> _repository = new();
    private readonly Mock<IBlobStoreFactory> _factory = new();
    private readonly Mock<IBlobStore> _store = new();

    [Fact]
    public async Task UploadHandler_WhenBeginFails_ShouldReturnErrors()
    {
        _bus.Setup(b =>
                b.InvokeAsync<ErrorOr<BeginUploadResponse>>(
                    It.IsAny<BeginUploadEndpointCommand>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()
                )
            )
            .ReturnsAsync(global::FileStorage.Domain.DomainErrors.Files.InvalidFileType(".exe"));

        ErrorOr<FileUploadResponse> result = await UploadFileCommandHandler.HandleAsync(
            new UploadFileCommand(
                new UploadFileRequest(
                    new MemoryStream([1, 2]),
                    "file.exe",
                    "application/octet-stream",
                    2,
                    null
                )
            ),
            _bus.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeTrue();
    }

    [Fact]
    public async Task DownloadHandler_WhenBlobMissing_ShouldMapToFileNotFound()
    {
        Guid id = Guid.NewGuid();
        StoredFile entity = StoredFile.Create("a.png", "deadbeef", "local", "image/png", 10, null);
        entity.Id = id;
        entity.TenantId = Guid.NewGuid();
        _repository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        _factory.Setup(f => f.Get("local")).Returns(_store.Object);
        _store
            .Setup(s =>
                s.OpenReadAsync(entity.TenantId, entity.Sha256, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(global::FileStorage.Domain.DomainErrors.Files.FileNotFound("blob"));

        ErrorOr<FileDownloadResult> result = await DownloadFileQueryHandler.HandleAsync(
            new DownloadFileQuery(new DownloadFileRequest(id)),
            _repository.Object,
            _factory.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteHandler_WhenEntityExists_ShouldMarkDeletedAndEmitMaybeDeleteMessage()
    {
        Guid id = Guid.NewGuid();
        StoredFile entity = StoredFile.Create("a.png", "hash", "local", "image/png", 10, null);
        entity.Id = id;
        entity.TenantId = Guid.NewGuid();
        await using FileStorageDbContext db = CreateDbContext();
        db.StoredFiles.Add(entity);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        (ErrorOr<Success> result, OutgoingMessages messages) =
            await DeleteFileCommandHandler.HandleAsync(
                new DeleteFileCommand(id),
                db,
                TestContext.Current.CancellationToken
            );

        result.IsError.ShouldBeFalse();
        messages.OfType<MaybeDeleteBlobCommand>().ShouldHaveSingleItem();
        entity.IsDeleted.ShouldBeTrue();
    }

    private static FileStorageDbContext CreateDbContext()
    {
        DbContextOptions<FileStorageDbContext> opts =
            new DbContextOptionsBuilder<FileStorageDbContext>()
                .UseInMemoryDatabase($"file-storage-handler-{Guid.NewGuid():N}")
                .Options;
        return new FileStorageDbContext(
            opts,
            DesignTimeServices.TenantProvider,
            DesignTimeServices.ActorProvider,
            TimeProvider.System,
            DesignTimeServices.AuditableEntityStateManager
        );
    }
}
