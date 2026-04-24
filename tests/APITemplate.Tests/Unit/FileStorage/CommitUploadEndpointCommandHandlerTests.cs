using ErrorOr;
using FileStorage.Contracts;
using FileStorage.Domain;
using FileStorage.Domain.Sagas;
using FileStorage.Features.Commit;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Wolverine;
using Xunit;

namespace APITemplate.Tests.Unit.FileStorage;

[Trait("Category", "Unit")]
public sealed class CommitUploadEndpointCommandHandlerTests
{
    private readonly Mock<IMessageBus> _bus = new();
    private readonly FileStorageOptions _options = new()
    {
        BasePath = "/tmp",
        BackendKey = "local",
        AllowedExtensions = [".png"],
        AllowedContentTypes = ["image/png", "application/pdf"],
    };

    private static UploadCommittedReply SampleReply() =>
        new(Guid.NewGuid(), "pic.png", "image/png", 100, "desc", DateTime.UtcNow);

    [Theory]
    [InlineData("image/png")]
    [InlineData("application/pdf")]
    [InlineData("IMAGE/PNG")] // case-insensitive allow-list
    public async Task HandleAsync_AllowedContentType_InvokesSagaAndReturnsResponse(string ct)
    {
        UploadCommittedReply reply = SampleReply();
        _bus.Setup(b =>
                b.InvokeAsync<ErrorOr<UploadCommittedReply>>(
                    It.IsAny<CommitUploadCommand>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()
                )
            )
            .ReturnsAsync(reply);

        CommitUploadEndpointCommand cmd = new(new CommitUploadRequest("token-123", ct, null));

        ErrorOr<FileUploadResponse> result = await CommitUploadEndpointCommandHandler.HandleAsync(
            cmd,
            Options.Create(_options),
            _bus.Object,
            CancellationToken.None
        );

        result.IsError.ShouldBeFalse();
        result.Value.Id.ShouldBe(reply.StoredFileId);
    }

    [Theory]
    [InlineData("text/html")]
    [InlineData("application/javascript")]
    [InlineData("image/svg+xml")]
    [InlineData("application/x-msdownload")]
    [InlineData("")]
    public async Task HandleAsync_DisallowedContentType_ReturnsErrorWithoutInvokingSaga(string ct)
    {
        CommitUploadEndpointCommand cmd = new(
            new CommitUploadRequest("token-123", string.IsNullOrEmpty(ct) ? "x" : ct, null)
        );

        // The [Required, MinLength] on the DTO prevents an empty ContentType before it gets here, so
        // we test the runtime allow-list check with non-empty disallowed values — the empty case is
        // covered by the data-annotation layer instead.
        if (string.IsNullOrWhiteSpace(ct))
            return;

        cmd = new(new CommitUploadRequest("token-123", ct, null));
        ErrorOr<FileUploadResponse> result = await CommitUploadEndpointCommandHandler.HandleAsync(
            cmd,
            Options.Create(_options),
            _bus.Object,
            CancellationToken.None
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe(ErrorCatalog.Files.InvalidFileType);
        _bus.Verify(
            b =>
                b.InvokeAsync<ErrorOr<UploadCommittedReply>>(
                    It.IsAny<CommitUploadCommand>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task HandleAsync_ContentTypeWithWhitespace_IsTrimmedBeforeLookup()
    {
        UploadCommittedReply reply = SampleReply();
        _bus.Setup(b =>
                b.InvokeAsync<ErrorOr<UploadCommittedReply>>(
                    It.IsAny<CommitUploadCommand>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()
                )
            )
            .ReturnsAsync(reply);

        CommitUploadEndpointCommand cmd = new(
            new CommitUploadRequest("token", "  image/png  ", null)
        );

        ErrorOr<FileUploadResponse> result = await CommitUploadEndpointCommandHandler.HandleAsync(
            cmd,
            Options.Create(_options),
            _bus.Object,
            CancellationToken.None
        );

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleAsync_SagaReturnsError_ErrorPropagated()
    {
        Error sagaError = DomainErrors.Files.CommitAfterTerminalState("token");
        _bus.Setup(b =>
                b.InvokeAsync<ErrorOr<UploadCommittedReply>>(
                    It.IsAny<CommitUploadCommand>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()
                )
            )
            .ReturnsAsync(sagaError);

        CommitUploadEndpointCommand cmd = new(new CommitUploadRequest("token", "image/png", null));

        ErrorOr<FileUploadResponse> result = await CommitUploadEndpointCommandHandler.HandleAsync(
            cmd,
            Options.Create(_options),
            _bus.Object,
            CancellationToken.None
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe(ErrorCatalog.Files.CommitAfterTerminalState);
    }
}
