using Ardalis.Specification;
using FileStorage.Domain;
using FileStorage.Domain.Sagas;
using FileStorage.Domain.Storage;
using FileStorage.Features.Delete;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.FileStorage;

public sealed class MaybeDeleteBlobHandlerTests
{
    private readonly Mock<IStoredFileRepository> _repository = new();
    private readonly Mock<IBlobStoreFactory> _factory = new();
    private readonly Mock<IBlobStore> _store = new();

    public MaybeDeleteBlobHandlerTests()
    {
        _factory.Setup(f => f.Get(It.IsAny<string>())).Returns(_store.Object);
    }

    [Fact]
    public async Task RefcountZero_DeletesBlob()
    {
        _repository
            .Setup(r =>
                r.CountAsync(
                    It.IsAny<ActiveStoredFilesBySha256AndTenantSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(0);

        Guid tenant = Guid.NewGuid();
        await MaybeDeleteBlobHandler.HandleAsync(
            new MaybeDeleteBlobCommand(tenant, "abc", "local"),
            _repository.Object,
            _factory.Object,
            NullLogger<MaybeDeleteBlobHandler>.Instance,
            CancellationToken.None
        );

        _store.Verify(s => s.DeleteAsync(tenant, "abc", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    public async Task RefcountPositive_SkipsDelete(int refcount)
    {
        _repository
            .Setup(r =>
                r.CountAsync(It.IsAny<ISpecification<StoredFile>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(refcount);

        await MaybeDeleteBlobHandler.HandleAsync(
            new MaybeDeleteBlobCommand(Guid.NewGuid(), "abc", "local"),
            _repository.Object,
            _factory.Object,
            NullLogger<MaybeDeleteBlobHandler>.Instance,
            CancellationToken.None
        );

        _store.Verify(
            s => s.DeleteAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Theory]
    [InlineData("local")]
    [InlineData("s3")]
    [InlineData("azure-blob")]
    public async Task RefcountZero_UsesCorrectBackend(string backendKey)
    {
        _repository
            .Setup(r =>
                r.CountAsync(It.IsAny<ISpecification<StoredFile>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(0);

        await MaybeDeleteBlobHandler.HandleAsync(
            new MaybeDeleteBlobCommand(Guid.NewGuid(), "abc", backendKey),
            _repository.Object,
            _factory.Object,
            NullLogger<MaybeDeleteBlobHandler>.Instance,
            CancellationToken.None
        );

        _factory.Verify(f => f.Get(backendKey), Times.Once);
    }

    [Fact]
    public async Task RefcountZero_PropagatesCancellation()
    {
        CancellationTokenSource cts = new();
        cts.Cancel();

        _repository
            .Setup(r =>
                r.CountAsync(It.IsAny<ISpecification<StoredFile>>(), It.IsAny<CancellationToken>())
            )
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await MaybeDeleteBlobHandler.HandleAsync(
                new MaybeDeleteBlobCommand(Guid.NewGuid(), "abc", "local"),
                _repository.Object,
                _factory.Object,
                NullLogger<MaybeDeleteBlobHandler>.Instance,
                cts.Token
            )
        );
    }
}
