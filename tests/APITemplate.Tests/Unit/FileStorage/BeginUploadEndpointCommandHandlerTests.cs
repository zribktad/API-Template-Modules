using System.Text;
using ErrorOr;
using FileStorage.Contracts;
using FileStorage.Domain;
using FileStorage.Domain.Sagas;
using FileStorage.Domain.Storage;
using FileStorage.Features.Staging;
using Microsoft.Extensions.Options;
using Moq;
using SharedKernel.Application.Context;
using Shouldly;
using Wolverine;
using Xunit;

namespace APITemplate.Tests.Unit.FileStorage;

public sealed class BeginUploadEndpointCommandHandlerTests
{
    private readonly Mock<IBlobStoreFactory> _factory = new();
    private readonly Mock<IBlobStore> _store = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<IMessageBus> _bus = new();
    private readonly FileStorageOptions _options = new()
    {
        BasePath = "/tmp",
        MaxFileSizeBytes = 1024,
        AllowedExtensions = [".png"],
        BackendKey = "local",
    };

    public BeginUploadEndpointCommandHandlerTests()
    {
        _factory.Setup(f => f.Get("local")).Returns(_store.Object);
        _tenantProvider.SetupGet(t => t.TenantId).Returns(Guid.NewGuid());
    }

    [Theory]
    [InlineData("malware.exe")]
    [InlineData("script.js")]
    [InlineData("no-extension-at-all")]
    [InlineData("weird.PNG.exe")]
    [InlineData("trailing-dot.")]
    public async Task HandleAsync_DisallowedExtension_ReturnsErrorAndDoesNotTouchStore(
        string fileName
    )
    {
        BeginUploadEndpointCommand cmd = new(
            new BeginUploadRequest(new MemoryStream(), fileName, 10)
        );

        ErrorOr<BeginUploadResponse> result = await BeginUploadEndpointCommandHandler.HandleAsync(
            cmd,
            _factory.Object,
            Options.Create(_options),
            _tenantProvider.Object,
            _bus.Object,
            CancellationToken.None
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe(ErrorCatalog.Files.InvalidFileType);
        _store.Verify(
            s => s.WriteStagingAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Theory]
    [InlineData(1025)]
    [InlineData(5000)]
    [InlineData(long.MaxValue)]
    public async Task HandleAsync_ClientDeclaredSizeOverLimit_FailsBeforeStaging(long size)
    {
        BeginUploadEndpointCommand cmd = new(
            new BeginUploadRequest(new MemoryStream(), "pic.png", size)
        );

        ErrorOr<BeginUploadResponse> result = await BeginUploadEndpointCommandHandler.HandleAsync(
            cmd,
            _factory.Object,
            Options.Create(_options),
            _tenantProvider.Object,
            _bus.Object,
            CancellationToken.None
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe(ErrorCatalog.Files.FileTooLarge);
        _store.Verify(
            s => s.WriteStagingAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task HandleAsync_WriteStagingThrowsFileTooLarge_MapsToValidationError()
    {
        MemoryStream stream = new(new byte[10]);
        _store
            .Setup(s => s.WriteStagingAsync(stream, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileTooLargeException(_options.MaxFileSizeBytes));

        BeginUploadEndpointCommand cmd = new(new BeginUploadRequest(stream, "pic.png", 10));

        ErrorOr<BeginUploadResponse> result = await BeginUploadEndpointCommandHandler.HandleAsync(
            cmd,
            _factory.Object,
            Options.Create(_options),
            _tenantProvider.Object,
            _bus.Object,
            CancellationToken.None
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe(ErrorCatalog.Files.FileTooLarge);
    }

    [Fact]
    public async Task HandleAsync_FileTooLarge_ReturnsErrorBeforeStaging()
    {
        BeginUploadEndpointCommand cmd = new(
            new BeginUploadRequest(new MemoryStream(), "pic.png", _options.MaxFileSizeBytes + 1)
        );

        ErrorOr<BeginUploadResponse> result = await BeginUploadEndpointCommandHandler.HandleAsync(
            cmd,
            _factory.Object,
            Options.Create(_options),
            _tenantProvider.Object,
            _bus.Object,
            CancellationToken.None
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe(ErrorCatalog.Files.FileTooLarge);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_EmptyFileName_ReturnsInvalidFileTypeAndDoesNotTouchStore(
        string fileName
    )
    {
        BeginUploadEndpointCommand cmd = new(
            new BeginUploadRequest(new MemoryStream(), fileName, 10)
        );

        ErrorOr<BeginUploadResponse> result = await BeginUploadEndpointCommandHandler.HandleAsync(
            cmd,
            _factory.Object,
            Options.Create(_options),
            _tenantProvider.Object,
            _bus.Object,
            CancellationToken.None
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe(ErrorCatalog.Files.InvalidFileType);
        _store.Verify(
            s => s.WriteStagingAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task HandleAsync_UppercaseExtension_IsAcceptedWhenWhitelistIsLowercase()
    {
        // Mirrors the post-normalization state produced by FileStorageModule.PostConfigure:
        // whitelist entries are already lowercased, and the handler lowercases the incoming
        // extension, so "pic.PNG" matches ".png" without a case-insensitive comparer.
        MemoryStream stream = new([1, 2, 3, 4, 5]);
        StagingResult staging = new("/tmp/staging/abc", "deadbeef", 5);
        _store
            .Setup(s => s.WriteStagingAsync(stream, It.IsAny<CancellationToken>()))
            .ReturnsAsync(staging);

        BeginUploadEndpointCommand cmd = new(new BeginUploadRequest(stream, "pic.PNG", 5));

        ErrorOr<BeginUploadResponse> result = await BeginUploadEndpointCommandHandler.HandleAsync(
            cmd,
            _factory.Object,
            Options.Create(_options),
            _tenantProvider.Object,
            _bus.Object,
            CancellationToken.None
        );

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleAsync_HappyPath_StagesAndPublishesBeginUploadCommand()
    {
        MemoryStream stream = new(Encoding.UTF8.GetBytes("hello"));
        StagingResult staging = new("/tmp/staging/abc", "deadbeef", 5);
        _store
            .Setup(s => s.WriteStagingAsync(stream, It.IsAny<CancellationToken>()))
            .ReturnsAsync(staging);

        BeginUploadEndpointCommand cmd = new(new BeginUploadRequest(stream, "pic.png", 5));

        ErrorOr<BeginUploadResponse> result = await BeginUploadEndpointCommandHandler.HandleAsync(
            cmd,
            _factory.Object,
            Options.Create(_options),
            _tenantProvider.Object,
            _bus.Object,
            CancellationToken.None
        );

        result.IsError.ShouldBeFalse();
        result.Value.Sha256.ShouldBe("deadbeef");
        result.Value.SizeBytes.ShouldBe(5);
        result.Value.UploadToken.ShouldNotBeNullOrWhiteSpace();

        // InvokeAsync awaits saga creation before the response returns; ScheduleAsync is an extension
        // method over PublishAsync(msg, DeliveryOptions{ScheduleDelay}), verified indirectly here by
        // the Invoke being recorded. Full scheduled-timeout delivery is covered by integration tests.
        _bus.Verify(
            b =>
                b.InvokeAsync(
                    It.IsAny<BeginUploadCommand>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()
                ),
            Times.Once
        );
    }
}
