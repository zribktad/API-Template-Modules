using global::ProductCatalog;
using APITemplate.Tests.Unit.Helpers;
using APITemplate.Tests.Unit.Infrastructure;
using ErrorOr;
using global::ProductCatalog.Features.ProductData.CreateImageProductData;
using global::ProductCatalog.Features.ProductData.CreateVideoProductData;
using global::ProductCatalog.Features.ProductData.DeleteProductData;
using global::ProductCatalog.Interfaces;
using Moq;
using SharedKernel.Application.Context;
using SharedKernel.Domain.Interfaces;
using Shouldly;
using Wolverine;
using Xunit;
using CacheTags = global::SharedKernel.Contracts.Events.CacheTags;

namespace APITemplate.Tests.Unit.ProductCatalog;

[Trait("Category", "Unit")]
public sealed class ProductDataCommandHandlersTests
{
    private readonly Mock<IProductDataRepository> _repository = new();
    private readonly Mock<IProductDataLinkRepository> _linkRepository = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<IActorProvider> _actorProvider = new();
    private readonly Mock<IUnitOfWork<ProductCatalogDbMarker>> _unitOfWork = new();

    public ProductDataCommandHandlersTests()
    {
        UnitOfWorkTestHelper.SetupTransactionPassthrough(_unitOfWork);
    }

    [Fact]
    public async Task CreateImage_ShouldPersistEntityAndEmitProductDataCacheTag()
    {
        Guid tenantId = Guid.NewGuid();
        _tenantProvider.SetupGet(x => x.TenantId).Returns(tenantId);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        CreateImageProductDataRequest request = new("img", "d", 100, 200, "png", 512);
        _repository
            .Setup(r =>
                r.CreateAsync(
                    It.IsAny<global::ProductCatalog.Entities.ProductData.ProductData>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(DomainTestDataFactory.ImageProductData(tenantId: tenantId, title: "img"));

        (
            ErrorOr<global::ProductCatalog.Features.ProductData.Shared.ProductDataResponse> result,
            OutgoingMessages messages
        ) = await CreateImageProductDataCommandHandler.HandleAsync(
            new CreateImageProductDataCommand(request),
            _repository.Object,
            _tenantProvider.Object,
            new FakeTimeProvider(now),
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        messages.ShouldContainSingleCacheTag(CacheTags.ProductData);
    }

    [Fact]
    public async Task CreateVideo_ShouldPersistEntityAndEmitProductDataCacheTag()
    {
        Guid tenantId = Guid.NewGuid();
        _tenantProvider.SetupGet(x => x.TenantId).Returns(tenantId);
        CreateVideoProductDataRequest request = new("vid", null, 30, "1080p", "mp4", 1024);
        _repository
            .Setup(r =>
                r.CreateAsync(
                    It.IsAny<global::ProductCatalog.Entities.ProductData.ProductData>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(DomainTestDataFactory.VideoProductData(tenantId: tenantId, title: "vid"));

        (_, OutgoingMessages messages) = await CreateVideoProductDataCommandHandler.HandleAsync(
            new CreateVideoProductDataCommand(request),
            _repository.Object,
            _tenantProvider.Object,
            TimeProvider.System,
            TestContext.Current.CancellationToken
        );

        messages.ShouldContainSingleCacheTag(CacheTags.ProductData);
    }

    [Fact]
    public async Task DeleteLoad_ShouldCaptureTenantActorAndTimestamp()
    {
        Guid tenantId = Guid.NewGuid();
        Guid actorId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        _tenantProvider.SetupGet(x => x.TenantId).Returns(tenantId);
        _actorProvider.SetupGet(x => x.ActorId).Returns(actorId);

        (
            HandlerContinuation continuation,
            DeleteProductDataCommandHandler.DeleteProductDataState? state,
            _
        ) = await DeleteProductDataCommandHandler.LoadAsync(
            new DeleteProductDataCommand(Guid.NewGuid()),
            _tenantProvider.Object,
            _actorProvider.Object,
            new FakeTimeProvider(now),
            TestContext.Current.CancellationToken
        );

        continuation.ShouldBe(HandlerContinuation.Continue);
        state.ShouldNotBeNull();
        state!.TenantId.ShouldBe(tenantId);
        state.ActorId.ShouldBe(actorId);
        state.DeletedAtUtc.ShouldBe(now.UtcDateTime);
    }

    [Fact]
    public async Task DeleteHandle_ShouldSoftDeleteLinksProductDataAndEmitCacheInvalidations()
    {
        Guid id = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        Guid actorId = Guid.NewGuid();
        DateTime deletedAt = DateTime.UtcNow;
        DeleteProductDataCommandHandler.DeleteProductDataState state = new(
            tenantId,
            actorId,
            deletedAt
        );
        _repository
            .Setup(r => r.SoftDeleteAsync(id, actorId, deletedAt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        (ErrorOr<Success> result, OutgoingMessages messages) =
            await DeleteProductDataCommandHandler.HandleAsync(
                new DeleteProductDataCommand(id),
                state,
                _linkRepository.Object,
                _repository.Object,
                _unitOfWork.Object,
                TestContext.Current.CancellationToken
            );

        result.IsError.ShouldBeFalse();
        _linkRepository.Verify(
            r =>
                r.SoftDeleteActiveLinksForProductDataAsync(
                    id,
                    tenantId,
                    actorId,
                    deletedAt,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        _repository.Verify(
            r => r.SoftDeleteAsync(id, actorId, deletedAt, It.IsAny<CancellationToken>()),
            Times.Once
        );
        messages.ShouldContainCacheTags([CacheTags.ProductData, CacheTags.Products]);
    }

    [Fact]
    public async Task DeleteHandle_WhenProductDataMissing_ShouldReturnNotFoundWithoutDeletingLinks()
    {
        Guid id = Guid.NewGuid();
        DeleteProductDataCommandHandler.DeleteProductDataState state = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow
        );
        _repository
            .Setup(r =>
                r.SoftDeleteAsync(
                    id,
                    state.ActorId,
                    state.DeletedAtUtc,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(false);

        (ErrorOr<Success> result, OutgoingMessages messages) =
            await DeleteProductDataCommandHandler.HandleAsync(
                new DeleteProductDataCommand(id),
                state,
                _linkRepository.Object,
                _repository.Object,
                _unitOfWork.Object,
                TestContext.Current.CancellationToken
            );

        result.IsError.ShouldBeTrue();
        messages.ShouldBeEmpty();
        _linkRepository.Verify(
            r =>
                r.SoftDeleteActiveLinksForProductDataAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }
}
