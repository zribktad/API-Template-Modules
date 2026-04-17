using ErrorOr;
using FileStorage.Contracts;
using FileStorage.Domain;
using FileStorage.Domain.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.FileStorage;

public sealed class FileUploadWorkflowTests
{
    private readonly Mock<IFileStorageService> _storage = new();
    private readonly FileStorageOptions _options = new()
    {
        BasePath = "/tmp",
        MaxFileSizeBytes = 1024,
        AllowedExtensions = [".png", ".jpg"],
    };

    private FileUploadWorkflow CreateSut()
    {
        return new FileUploadWorkflow(
            _storage.Object,
            Options.Create(_options),
            NullLogger<FileUploadWorkflow>.Instance
        );
    }

    private static UploadFileRequest RequestFor(
        string fileName,
        long sizeBytes,
        string contentType = "image/png"
    )
    {
        return new UploadFileRequest(
            Stream.Null,
            fileName,
            contentType,
            sizeBytes,
            Description: null
        );
    }

    [Fact]
    public async Task PrepareAsync_WhenExtensionNotAllowed_ReturnsValidationError()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        UploadFileRequest request = RequestFor("malware.exe", 100);

        ErrorOr<StoredFile> result = await CreateSut().PrepareAsync(request, ct);

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
        result.FirstError.Code.ShouldBe(ErrorCatalog.Files.InvalidFileType);
        _storage.Verify(
            s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task PrepareAsync_WhenNoExtension_ReturnsValidationError()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        UploadFileRequest request = RequestFor("noext", 100);

        ErrorOr<StoredFile> result = await CreateSut().PrepareAsync(request, ct);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe(ErrorCatalog.Files.InvalidFileType);
    }

    [Fact]
    public async Task PrepareAsync_WhenFileTooLarge_ReturnsValidationError()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        UploadFileRequest request = RequestFor("big.png", _options.MaxFileSizeBytes + 1);

        ErrorOr<StoredFile> result = await CreateSut().PrepareAsync(request, ct);

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
        result.FirstError.Code.ShouldBe(ErrorCatalog.Files.FileTooLarge);
        _storage.Verify(
            s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task PrepareAsync_WhenValid_SavesToStorageAndBuildsEntity()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        UploadFileRequest request = new(Stream.Null, "pic.PNG", "image/png", 100, "desc");
        _storage
            .Setup(s => s.SaveAsync(request.FileStream, request.FileName, ct))
            .ReturnsAsync(new FileStorageResult("/tmp/pic.png", 100));

        ErrorOr<StoredFile> result = await CreateSut().PrepareAsync(request, ct);

        result.IsError.ShouldBeFalse();
        StoredFile entity = result.Value;
        entity.OriginalFileName.ShouldBe("pic.PNG");
        entity.StoragePath.ShouldBe("/tmp/pic.png");
        entity.ContentType.ShouldBe("image/png");
        entity.SizeBytes.ShouldBe(100);
        entity.Description.ShouldBe("desc");
        entity.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task PrepareAsync_ExtensionCheck_IsCaseInsensitive()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        UploadFileRequest request = new(Stream.Null, "PHOTO.PNG", "image/png", 100, null);
        _storage
            .Setup(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), ct))
            .ReturnsAsync(new FileStorageResult("/tmp/photo.png", 100));

        ErrorOr<StoredFile> result = await CreateSut().PrepareAsync(request, ct);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    public async Task RollbackAsync_WhenStorageDeleteThrows_SwallowsExceptionToPreserveOriginal()
    {
        StoredFile entity = StoredFile.Create("file.png", "/tmp/file.png", "image/png", 100, null);
        _storage
            .Setup(s => s.DeleteAsync("/tmp/file.png", CancellationToken.None))
            .ThrowsAsync(new IOException("storage gone"));

        await Should.NotThrowAsync(() => CreateSut().RollbackAsync(entity));

        _storage.Verify(s => s.DeleteAsync("/tmp/file.png", CancellationToken.None), Times.Once);
    }
}
