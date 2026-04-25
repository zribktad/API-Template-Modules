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
using CacheTags = global::ProductCatalog.Common.Events.CacheTags;

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
    public async Task DeleteLoad_WhenTenantMismatch_ShouldStop()
    {
        Guid id = Guid.NewGuid();
        _tenantProvider.SetupGet(x => x.TenantId).Returns(Guid.NewGuid());
        _repository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DomainTestDataFactory.ImageProductData(id: id, tenantId: Guid.NewGuid()));

        (
            HandlerContinuation continuation,
            DeleteProductDataCommandHandler.DeleteProductDataState? state,
            _
        ) = await DeleteProductDataCommandHandler.LoadAsync(
            new DeleteProductDataCommand(id),
            _repository.Object,
            _tenantProvider.Object,
            _actorProvider.Object,
            TimeProvider.System,
            TestContext.Current.CancellationToken
        );

        continuation.ShouldBe(HandlerContinuation.Stop);
        state.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteHandle_ShouldSoftDeleteLinksAndEmitCascadeMessages()
    {
        Guid id = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        Guid actorId = Guid.NewGuid();
        DateTime deletedAt = DateTime.UtcNow;
        DeleteProductDataCommandHandler.DeleteProductDataState state = new(
            DomainTestDataFactory.ImageProductData(id: id, tenantId: tenantId),
            tenantId,
            actorId,
            deletedAt
        );

        (ErrorOr<Success> result, OutgoingMessages messages) =
            await DeleteProductDataCommandHandler.HandleAsync(
                new DeleteProductDataCommand(id),
                state,
                _linkRepository.Object,
                _unitOfWork.Object,
                TestContext.Current.CancellationToken
            );

        result.IsError.ShouldBeFalse();
        _linkRepository.Verify(
            r => r.SoftDeleteActiveLinksForProductDataAsync(id, It.IsAny<CancellationToken>()),
            Times.Once
        );
        messages.ShouldContainCacheTags([CacheTags.ProductData, CacheTags.Products]);
    }
}
