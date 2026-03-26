using APITemplate.Application.Common.Context;
using APITemplate.Application.Common.Events;
using APITemplate.Application.Common.Resilience;
using APITemplate.Application.Features.ProductData;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using APITemplate.Domain.Options;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Moq;
using Polly;
using Polly.Registry;
using Shouldly;
using Wolverine;
using Xunit;

namespace APITemplate.Tests.Unit.Handlers;

public class ProductDataRequestHandlersTests
{
    private readonly Mock<IProductDataRepository> _repositoryMock;
    private readonly Mock<IProductDataLinkRepository> _productDataLinkRepositoryMock;
    private readonly Mock<ITenantProvider> _tenantProviderMock;
    private readonly Mock<IActorProvider> _actorProviderMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IMessageBus> _busMock;
    private readonly Mock<ILogger<DeleteProductDataCommandHandler>> _loggerMock;
    private readonly ResiliencePipelineRegistry<string> _registry;
    private readonly Guid _tenantId = Guid.NewGuid();

    public ProductDataRequestHandlersTests()
    {
        _repositoryMock = new Mock<IProductDataRepository>();
        _productDataLinkRepositoryMock = new Mock<IProductDataLinkRepository>();
        _tenantProviderMock = new Mock<ITenantProvider>();
        _actorProviderMock = new Mock<IActorProvider>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _busMock = new Mock<IMessageBus>();
        _loggerMock = new Mock<ILogger<DeleteProductDataCommandHandler>>();
        _tenantProviderMock.SetupGet(x => x.TenantId).Returns(_tenantId);
        _actorProviderMock.SetupGet(x => x.ActorId).Returns(Guid.NewGuid());
        _unitOfWorkMock.SetupImmediateTransactionExecution();

        _registry = new ResiliencePipelineRegistry<string>();
        _registry.TryAddBuilder(ResiliencePipelineKeys.MongoProductDataDelete, (builder, _) => { });
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllItems()
    {
        var ct = TestContext.Current.CancellationToken;
        var items = new List<ProductData>
        {
            new ImageProductData
            {
                Title = "Photo",
                Width = 1920,
                Height = 1080,
                Format = "jpg",
                FileSizeBytes = 500000,
            },
            new VideoProductData
            {
                Title = "Clip",
                DurationSeconds = 30,
                Resolution = "1080p",
                Format = "mp4",
                FileSizeBytes = 5000000,
            },
        };

        _repositoryMock
            .Setup(r => r.GetAllAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        var result = await GetProductDataQueryHandler.HandleAsync(
            new GetProductDataQuery(null),
            _repositoryMock.Object,
            ct
        );

        result.IsError.ShouldBeFalse();
        result.Value.Count.ShouldBe(2);
        result.Value[0].Type.ShouldBe("image");
        result.Value[1].Type.ShouldBe("video");
    }

    [Fact]
    public async Task GetAllAsync_WithTypeFilter_PassesTypeToRepository()
    {
        var ct = TestContext.Current.CancellationToken;
        _repositoryMock
            .Setup(r => r.GetAllAsync("image", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ImageProductData
                {
                    Title = "Photo",
                    Width = 100,
                    Height = 100,
                    Format = "png",
                    FileSizeBytes = 1000,
                },
            ]);

        var result = await GetProductDataQueryHandler.HandleAsync(
            new GetProductDataQuery("image"),
            _repositoryMock.Object,
            ct
        );

        result.IsError.ShouldBeFalse();
        result.Value.Count.ShouldBe(1);
        result.Value[0].Type.ShouldBe("image");
        _repositoryMock.Verify(
            r => r.GetAllAsync("image", It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task GetAllAsync_WhenEmpty_ReturnsEmptyList()
    {
        var ct = TestContext.Current.CancellationToken;
        _repositoryMock
            .Setup(r => r.GetAllAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await GetProductDataQueryHandler.HandleAsync(
            new GetProductDataQuery(null),
            _repositoryMock.Object,
            ct
        );

        result.IsError.ShouldBeFalse();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_ReturnsResponse()
    {
        var ct = TestContext.Current.CancellationToken;
        var image = new ImageProductData
        {
            TenantId = _tenantId,
            Title = "Banner",
            Width = 800,
            Height = 600,
            Format = "jpg",
            FileSizeBytes = 200000,
        };

        _repositoryMock
            .Setup(r => r.GetByIdAsync(image.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(image);

        var result = await GetProductDataByIdQueryHandler.HandleAsync(
            new GetProductDataByIdQuery(image.Id),
            _repositoryMock.Object,
            _tenantProviderMock.Object,
            ct
        );

        result.IsError.ShouldBeFalse();
        result.Value.Id.ShouldBe(image.Id);
        result.Value.Type.ShouldBe("image");
        result.Value.Title.ShouldBe("Banner");
        var imageResult = result.Value.ShouldBeOfType<ImageProductDataResponse>();
        imageResult.Width.ShouldBe(800);
        imageResult.Height.ShouldBe(600);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductData?)null);

        var result = await GetProductDataByIdQueryHandler.HandleAsync(
            new GetProductDataByIdQuery(Guid.NewGuid()),
            _repositoryMock.Object,
            _tenantProviderMock.Object,
            ct
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task CreateImageAsync_CreatesAndReturnsImageResponse()
    {
        var ct = TestContext.Current.CancellationToken;
        var request = new CreateImageProductDataRequest(
            "Banner",
            "A banner",
            1920,
            1080,
            "jpg",
            500000
        );

        _repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<ImageProductData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductData d, CancellationToken _) => d);

        var result = await CreateImageProductDataCommandHandler.HandleAsync(
            new CreateImageProductDataCommand(request),
            _repositoryMock.Object,
            _tenantProviderMock.Object,
            _busMock.Object,
            TimeProvider.System,
            ct
        );

        result.IsError.ShouldBeFalse();
        result.Value.Type.ShouldBe("image");
        result.Value.Title.ShouldBe("Banner");
        result.Value.Description.ShouldBe("A banner");
        var imageResult = result.Value.ShouldBeOfType<ImageProductDataResponse>();
        imageResult.Width.ShouldBe(1920);
        imageResult.Height.ShouldBe(1080);
        imageResult.Format.ShouldBe("jpg");
        imageResult.FileSizeBytes.ShouldBe(500000);

        _repositoryMock.Verify(
            r =>
                r.CreateAsync(
                    It.Is<ImageProductData>(e => e.Title == "Banner" && e.TenantId == _tenantId),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        _busMock.Verify(p => p.PublishAsync(It.IsAny<CacheInvalidationNotification>()), Times.Once);
    }

    [Fact]
    public async Task CreateVideoAsync_CreatesAndReturnsVideoResponse()
    {
        var ct = TestContext.Current.CancellationToken;
        var request = new CreateVideoProductDataRequest(
            "Intro",
            null,
            60,
            "1080p",
            "mp4",
            10000000
        );

        _repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<VideoProductData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductData d, CancellationToken _) => d);

        var result = await CreateVideoProductDataCommandHandler.HandleAsync(
            new CreateVideoProductDataCommand(request),
            _repositoryMock.Object,
            _tenantProviderMock.Object,
            _busMock.Object,
            TimeProvider.System,
            ct
        );

        result.IsError.ShouldBeFalse();
        result.Value.Type.ShouldBe("video");
        result.Value.Title.ShouldBe("Intro");
        result.Value.Description.ShouldBeNull();
        var videoResult = result.Value.ShouldBeOfType<VideoProductDataResponse>();
        videoResult.DurationSeconds.ShouldBe(60);
        videoResult.Resolution.ShouldBe("1080p");
        videoResult.Format.ShouldBe("mp4");
        videoResult.FileSizeBytes.ShouldBe(10000000);

        _repositoryMock.Verify(
            r =>
                r.CreateAsync(
                    It.Is<VideoProductData>(e => e.Title == "Intro" && e.TenantId == _tenantId),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        _busMock.Verify(p => p.PublishAsync(It.IsAny<CacheInvalidationNotification>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenProductDataMissing_ThrowsNotFoundException()
    {
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductData?)null);

        var result = await DeleteProductDataCommandHandler.HandleAsync(
            new DeleteProductDataCommand(Guid.NewGuid()),
            _repositoryMock.Object,
            _productDataLinkRepositoryMock.Object,
            _tenantProviderMock.Object,
            _actorProviderMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            TimeProvider.System,
            _registry,
            _loggerMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesLinksAndMongoDocument()
    {
        var id = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ImageProductData
                {
                    Id = id,
                    TenantId = _tenantId,
                    Title = "Image",
                }
            );

        await DeleteProductDataCommandHandler.HandleAsync(
            new DeleteProductDataCommand(id),
            _repositoryMock.Object,
            _productDataLinkRepositoryMock.Object,
            _tenantProviderMock.Object,
            _actorProviderMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            TimeProvider.System,
            _registry,
            _loggerMock.Object,
            TestContext.Current.CancellationToken
        );

        _productDataLinkRepositoryMock.Verify(
            r => r.SoftDeleteActiveLinksForProductDataAsync(id, It.IsAny<CancellationToken>()),
            Times.Once
        );
        _repositoryMock.Verify(
            r =>
                r.SoftDeleteAsync(
                    id,
                    It.IsAny<Guid>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        _unitOfWorkMock.Verify(
            u =>
                u.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TransactionOptions?>()
                ),
            Times.Once
        );
        _busMock.Verify(p => p.PublishAsync(It.IsAny<CacheInvalidationNotification>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenMongoSoftDeleteFails_LogsAndRethrows()
    {
        var id = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ImageProductData
                {
                    Id = id,
                    TenantId = _tenantId,
                    Title = "Image",
                }
            );
        _repositoryMock
            .Setup(r =>
                r.SoftDeleteAsync(
                    id,
                    It.IsAny<Guid>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("mongo failed"));

        var act = () =>
            DeleteProductDataCommandHandler.HandleAsync(
                new DeleteProductDataCommand(id),
                _repositoryMock.Object,
                _productDataLinkRepositoryMock.Object,
                _tenantProviderMock.Object,
                _actorProviderMock.Object,
                _unitOfWorkMock.Object,
                _busMock.Object,
                TimeProvider.System,
                _registry,
                _loggerMock.Object,
                TestContext.Current.CancellationToken
            );

        var ex = await Should.ThrowAsync<InvalidOperationException>(act);

        ex.Message.ShouldBe("mongo failed");
        _loggerMock.Verify(
            logger =>
                logger.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>(
                        (state, _) =>
                            state.ToString()!.Contains("Failed to soft-delete ProductData document")
                    ),
                    It.IsAny<InvalidOperationException>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
    }
}
