using APITemplate.Tests.Unit.Helpers;
using APITemplate.Tests.Unit.Infrastructure;
using BuildingBlocks.Application.Context;
using BuildingBlocks.Application.DTOs;
using BuildingBlocks.Domain.Interfaces;
using BuildingBlocks.Domain.Options;
using ErrorOr;
using Moq;
using ProductCatalog;
using ProductCatalog.Common.Events;
using ProductCatalog.Entities;
using ProductCatalog.Features.Product.DeleteProducts;
using ProductCatalog.Features.Product.Shared;
using ProductCatalog.Interfaces;
using ProductCatalog.ValueObjects;
using SharedKernel.Contracts.Events;
using Shouldly;
using Wolverine;
using Xunit;

namespace APITemplate.Tests.Unit.ProductCatalog;

[Trait("Category", "Unit")]
public sealed class DeleteProductsCommandHandlerTests
{
    private static readonly DateTime FixedDeletedAt = new(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IProductRepository> _productRepo = new();
    private readonly Mock<IProductDataLinkRepository> _linkRepo = new();
    private readonly Mock<IUnitOfWork<ProductCatalogDbMarker>> _unitOfWork = new();
    private readonly Mock<IActorProvider> _actorProvider = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<IIdGenerator> _idGenerator = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(FixedDeletedAt));

    public DeleteProductsCommandHandlerTests()
    {
        UnitOfWorkTestHelper.SetupTransactionPassthrough(_unitOfWork);

        _linkRepo
            .Setup(r =>
                r.BulkSoftDeleteByProductIdsAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(0);
    }

    private static Product MakeProduct(Guid? id = null, Guid? tenantId = null)
    {
        return new Product
        {
            Id = id ?? Guid.NewGuid(),
            Name = "Test Product",
            Price = Price.Zero,
            TenantId = tenantId ?? Guid.NewGuid(),
        };
    }

    // ── LoadAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_WhenAllIdsExist_ReturnsContinueWithState()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Product product = MakeProduct();
        Guid actorId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        _actorProvider.Setup(a => a.ActorId).Returns(actorId);
        _tenantProvider.Setup(t => t.TenantId).Returns(tenantId);
        _productRepo
            .Setup(r => r.ListAsync(It.IsAny<ProductsByIdsSpecification>(), ct))
            .ReturnsAsync([product]);

        (
            HandlerContinuation continuation,
            DeleteProductsCommandHandler.DeleteProductsState? state,
            OutgoingMessages _
        ) = await DeleteProductsCommandHandler.LoadAsync(
            new DeleteProductsCommand(new BatchDeleteRequest([product.Id])),
            _productRepo.Object,
            _actorProvider.Object,
            _tenantProvider.Object,
            _timeProvider,
            ct
        );

        continuation.ShouldBe(HandlerContinuation.Continue);
        state.ShouldNotBeNull();
        state!.ProductIds.ShouldContain(product.Id);
        state.TenantId.ShouldBe(tenantId);
        state.ActorId.ShouldBe(actorId);
        state.DeletedAtUtc.ShouldBe(FixedDeletedAt);
    }

    [Fact]
    public async Task LoadAsync_WhenSomeIdsMissing_ReturnsStopWithNullState()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _productRepo
            .Setup(r => r.ListAsync(It.IsAny<ProductsByIdsSpecification>(), ct))
            .ReturnsAsync([]);

        (
            HandlerContinuation continuation,
            DeleteProductsCommandHandler.DeleteProductsState? state,
            OutgoingMessages _
        ) = await DeleteProductsCommandHandler.LoadAsync(
            new DeleteProductsCommand(new BatchDeleteRequest([Guid.NewGuid()])),
            _productRepo.Object,
            _actorProvider.Object,
            _tenantProvider.Object,
            _timeProvider,
            ct
        );

        continuation.ShouldBe(HandlerContinuation.Stop);
        state.ShouldBeNull();
    }

    [Fact]
    public async Task LoadAsync_WithDuplicateIds_StoresDistinctProductIdsInState()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Product product = MakeProduct();
        _productRepo
            .Setup(r => r.ListAsync(It.IsAny<ProductsByIdsSpecification>(), ct))
            .ReturnsAsync([product]);

        (
            HandlerContinuation continuation,
            DeleteProductsCommandHandler.DeleteProductsState? state,
            OutgoingMessages _
        ) = await DeleteProductsCommandHandler.LoadAsync(
            new DeleteProductsCommand(new BatchDeleteRequest([product.Id, product.Id])),
            _productRepo.Object,
            _actorProvider.Object,
            _tenantProvider.Object,
            _timeProvider,
            ct
        );

        continuation.ShouldBe(HandlerContinuation.Continue);
        state.ShouldNotBeNull();
        state!.ProductIds.Count.ShouldBe(1);
        state.ProductIds.Single().ShouldBe(product.Id);
    }

    [Fact]
    public async Task LoadAsync_UsesProductsByIdsSpecificationWithoutLinks()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Product product = MakeProduct();
        _productRepo
            .Setup(r => r.ListAsync(It.IsAny<ProductsByIdsSpecification>(), ct))
            .ReturnsAsync([product]);

        await DeleteProductsCommandHandler.LoadAsync(
            new DeleteProductsCommand(new BatchDeleteRequest([product.Id])),
            _productRepo.Object,
            _actorProvider.Object,
            _tenantProvider.Object,
            _timeProvider,
            ct
        );

        // Must use the lightweight spec — no link eager-loading
        _productRepo.Verify(
            r => r.ListAsync(It.IsAny<ProductsByIdsSpecification>(), ct),
            Times.Once
        );
        _productRepo.Verify(
            r => r.ListAsync(It.IsAny<ProductsByIdsWithLinksSpecification>(), ct),
            Times.Never
        );
    }

    // ── HandleAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_PublishesProductsBatchSoftDeletedNotification()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid actorId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        Guid correlationId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        Product product = MakeProduct(tenantId: tenantId);
        DeleteProductsCommandHandler.DeleteProductsState state = new(
            [product.Id],
            tenantId,
            actorId,
            FixedDeletedAt
        );
        _idGenerator.Setup(g => g.NewId()).Returns(correlationId);

        (ErrorOr<BatchResponse> _, OutgoingMessages messages) =
            await DeleteProductsCommandHandler.HandleAsync(
                new DeleteProductsCommand(new BatchDeleteRequest([product.Id])),
                state,
                _productRepo.Object,
                _unitOfWork.Object,
                _linkRepo.Object,
                _idGenerator.Object,
                ct
            );

        ProductsBatchSoftDeletedNotification? notification = messages
            .OfType<ProductsBatchSoftDeletedNotification>()
            .SingleOrDefault();
        notification.ShouldNotBeNull();
        notification!.ProductIds.ShouldContain(product.Id);
        notification.TenantId.ShouldBe(tenantId);
        notification.ActorId.ShouldBe(actorId);
        notification.DeletedAtUtc.ShouldBe(FixedDeletedAt);
        notification.CorrelationId.ShouldBe(correlationId);
    }

    [Fact]
    public async Task HandleAsync_PublishesCacheInvalidations()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Product product = MakeProduct();
        DeleteProductsCommandHandler.DeleteProductsState state = new(
            [product.Id],
            Guid.NewGuid(),
            Guid.NewGuid(),
            FixedDeletedAt
        );

        (ErrorOr<BatchResponse> _, OutgoingMessages messages) =
            await DeleteProductsCommandHandler.HandleAsync(
                new DeleteProductsCommand(new BatchDeleteRequest([product.Id])),
                state,
                _productRepo.Object,
                _unitOfWork.Object,
                _linkRepo.Object,
                _idGenerator.Object,
                ct
            );

        messages.ShouldContainCacheTags([
            CacheTags.Products,
            CacheTags.Categories,
            CrossModuleCacheTags.Reviews,
        ]);
    }

    [Fact]
    public async Task HandleAsync_SoftDeletesLinksBeforeProductsInsideSingleTransaction()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid actorId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        Product product = MakeProduct(tenantId: tenantId);
        DeleteProductsCommandHandler.DeleteProductsState state = new(
            [product.Id],
            tenantId,
            actorId,
            FixedDeletedAt
        );
        List<string> callOrder = new List<string>();

        _linkRepo
            .Setup(r =>
                r.BulkSoftDeleteByProductIdsAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<DateTime>(),
                    ct
                )
            )
            .Callback(() => callOrder.Add("links"))
            .ReturnsAsync(1);
        _productRepo
            .Setup(r =>
                r.BulkSoftDeleteByIdsAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<DateTime>(),
                    ct
                )
            )
            .Callback(() => callOrder.Add("products"))
            .ReturnsAsync(1);

        await DeleteProductsCommandHandler.HandleAsync(
            new DeleteProductsCommand(new BatchDeleteRequest([product.Id])),
            state,
            _productRepo.Object,
            _unitOfWork.Object,
            _linkRepo.Object,
            _idGenerator.Object,
            ct
        );

        _unitOfWork.Verify(
            u =>
                u.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task>>(),
                    ct,
                    It.IsAny<TransactionOptions?>()
                ),
            Times.Once
        );
        callOrder.ShouldBe(["links", "products"]);
        _linkRepo.Verify(
            r =>
                r.BulkSoftDeleteByProductIdsAsync(
                    It.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(state.ProductIds)),
                    tenantId,
                    actorId,
                    FixedDeletedAt,
                    ct
                ),
            Times.Once
        );
        _productRepo.Verify(
            r =>
                r.BulkSoftDeleteByIdsAsync(
                    It.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(state.ProductIds)),
                    tenantId,
                    actorId,
                    FixedDeletedAt,
                    ct
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task HandleAsync_WhenLinkSoftDeleteFails_ShouldNotSoftDeleteProducts()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid actorId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        Product product = MakeProduct(tenantId: tenantId);
        DeleteProductsCommandHandler.DeleteProductsState state = new(
            [product.Id],
            tenantId,
            actorId,
            FixedDeletedAt
        );

        _linkRepo
            .Setup(r =>
                r.BulkSoftDeleteByProductIdsAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<DateTime>(),
                    ct
                )
            )
            .ThrowsAsync(new InvalidOperationException("link delete failed"));

        Func<Task> act = async () =>
        {
            await DeleteProductsCommandHandler.HandleAsync(
                new DeleteProductsCommand(new BatchDeleteRequest([product.Id])),
                state,
                _productRepo.Object,
                _unitOfWork.Object,
                _linkRepo.Object,
                _idGenerator.Object,
                ct
            );
        };

        await act.ShouldThrowAsync<InvalidOperationException>();
        _productRepo.Verify(
            r =>
                r.BulkSoftDeleteByIdsAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }
}
