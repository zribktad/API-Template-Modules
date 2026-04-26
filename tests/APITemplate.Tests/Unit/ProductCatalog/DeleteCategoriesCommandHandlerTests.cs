using APITemplate.Tests.Unit.Infrastructure;
using ErrorOr;
using Moq;
using ProductCatalog;
using ProductCatalog.Common.Events;
using ProductCatalog.Entities;
using ProductCatalog.Features.Category.DeleteCategories;
using ProductCatalog.Features.Category.Shared;
using ProductCatalog.Interfaces;
using SharedKernel.Application.Context;
using SharedKernel.Application.DTOs;
using SharedKernel.Domain.Interfaces;
using Shouldly;
using Wolverine;
using Xunit;

namespace APITemplate.Tests.Unit.ProductCatalog;

[Trait("Category", "Unit")]
public sealed class DeleteCategoriesCommandHandlerTests
{
    private static readonly DateTime FixedDeletedAt = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<ICategoryRepository> _categoryRepo = new();
    private readonly Mock<IProductRepository> _productRepo = new();
    private readonly Mock<IUnitOfWork<ProductCatalogDbMarker>> _unitOfWork = new();
    private readonly Mock<IActorProvider> _actorProvider = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    public DeleteCategoriesCommandHandlerTests()
    {
        UnitOfWorkTestHelper.SetupTransactionPassthrough(_unitOfWork);
    }

    [Fact]
    public async Task LoadAsync_WhenAllIdsExist_ReturnsContinueWithState()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid id = Guid.NewGuid();
        Guid actorId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        _actorProvider.Setup(a => a.ActorId).Returns(actorId);
        _tenantProvider.Setup(t => t.TenantId).Returns(tenantId);
        _categoryRepo
            .Setup(r => r.ListAsync(It.IsAny<CategoriesByIdsSpecification>(), ct))
            .ReturnsAsync([new Category { Id = id, Name = "Cat" }]);

        (HandlerContinuation continuation, DeleteCategoriesState? state, OutgoingMessages _) =
            await DeleteCategoriesCommandHandler.LoadAsync(
                new DeleteCategoriesCommand(new BatchDeleteRequest([id])),
                _categoryRepo.Object,
                _actorProvider.Object,
                _tenantProvider.Object,
                _timeProvider,
                ct
            );

        continuation.ShouldBe(HandlerContinuation.Continue);
        state.ShouldNotBeNull();
        state!.CategoryIds.ShouldContain(id);
        state.ActorId.ShouldBe(actorId);
        state.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public async Task LoadAsync_WhenSomeIdsMissing_ReturnsStopWithNullState()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _categoryRepo
            .Setup(r => r.ListAsync(It.IsAny<CategoriesByIdsSpecification>(), ct))
            .ReturnsAsync([]);

        (HandlerContinuation continuation, DeleteCategoriesState? state, OutgoingMessages _) =
            await DeleteCategoriesCommandHandler.LoadAsync(
                new DeleteCategoriesCommand(new BatchDeleteRequest([Guid.NewGuid()])),
                _categoryRepo.Object,
                _actorProvider.Object,
                _tenantProvider.Object,
                _timeProvider,
                ct
            );

        continuation.ShouldBe(HandlerContinuation.Stop);
        state.ShouldBeNull();
    }

    [Fact]
    public async Task LoadAsync_WithDuplicateIds_StoresDistinctCategoryIdsInState()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid id = Guid.NewGuid();
        _categoryRepo
            .Setup(r => r.ListAsync(It.IsAny<CategoriesByIdsSpecification>(), ct))
            .ReturnsAsync([new Category { Id = id, Name = "Cat" }]);

        (HandlerContinuation continuation, DeleteCategoriesState? state, OutgoingMessages _) =
            await DeleteCategoriesCommandHandler.LoadAsync(
                new DeleteCategoriesCommand(new BatchDeleteRequest([id, id])),
                _categoryRepo.Object,
                _actorProvider.Object,
                _tenantProvider.Object,
                _timeProvider,
                ct
            );

        continuation.ShouldBe(HandlerContinuation.Continue);
        state.ShouldNotBeNull();
        state!.CategoryIds.Count.ShouldBe(1);
        state.CategoryIds.Single().ShouldBe(id);
    }

    [Fact]
    public async Task HandleAsync_ClearsCategoryBeforeSoftDelete()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid id = Guid.NewGuid();
        Guid actorId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        DeleteCategoriesState state = new([id], tenantId, actorId, FixedDeletedAt);
        List<string> callOrder = [];

        _productRepo
            .Setup(r => r.ClearCategoryAsync(It.IsAny<IReadOnlyCollection<Guid>>(), ct))
            .Callback(() => callOrder.Add("clear"))
            .Returns(Task.CompletedTask);
        _categoryRepo
            .Setup(r =>
                r.BulkSoftDeleteByIdsAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    tenantId,
                    actorId,
                    FixedDeletedAt,
                    ct
                )
            )
            .Callback(() => callOrder.Add("softDelete"))
            .ReturnsAsync(1);

        (ErrorOr<BatchResponse> result, OutgoingMessages messages) =
            await DeleteCategoriesCommandHandler.HandleAsync(
                new DeleteCategoriesCommand(new BatchDeleteRequest([id])),
                state,
                _categoryRepo.Object,
                _productRepo.Object,
                _unitOfWork.Object,
                ct
            );

        result.IsError.ShouldBeFalse();
        callOrder.ShouldBe(["clear", "softDelete"]);
        messages.ShouldContainCacheTags([CacheTags.Categories, CacheTags.Products]);
    }

    [Fact]
    public async Task HandleAsync_CallsBulkSoftDeleteWithCorrectIdsAndMetadata()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid id = Guid.NewGuid();
        Guid actorId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        DeleteCategoriesState state = new([id], tenantId, actorId, FixedDeletedAt);

        _productRepo
            .Setup(r => r.ClearCategoryAsync(It.IsAny<IReadOnlyCollection<Guid>>(), ct))
            .Returns(Task.CompletedTask);
        _categoryRepo
            .Setup(r =>
                r.BulkSoftDeleteByIdsAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<DateTime>(),
                    ct
                )
            )
            .ReturnsAsync(1);

        (ErrorOr<BatchResponse> result, OutgoingMessages _) =
            await DeleteCategoriesCommandHandler.HandleAsync(
                new DeleteCategoriesCommand(new BatchDeleteRequest([id])),
                state,
                _categoryRepo.Object,
                _productRepo.Object,
                _unitOfWork.Object,
                ct
            );

        result.IsError.ShouldBeFalse();
        _categoryRepo.Verify(
            r =>
                r.BulkSoftDeleteByIdsAsync(
                    It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(id)),
                    tenantId,
                    actorId,
                    FixedDeletedAt,
                    ct
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task HandleAsync_WhenClearCategoryFails_ShouldNotSoftDeleteCategories()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid id = Guid.NewGuid();
        Guid actorId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        DeleteCategoriesState state = new([id], tenantId, actorId, FixedDeletedAt);

        _productRepo
            .Setup(r => r.ClearCategoryAsync(It.IsAny<IReadOnlyCollection<Guid>>(), ct))
            .ThrowsAsync(new InvalidOperationException("clear failed"));

        Func<Task> act = async () =>
        {
            await DeleteCategoriesCommandHandler.HandleAsync(
                new DeleteCategoriesCommand(new BatchDeleteRequest([id])),
                state,
                _categoryRepo.Object,
                _productRepo.Object,
                _unitOfWork.Object,
                ct
            );
        };

        await act.ShouldThrowAsync<InvalidOperationException>();
        _categoryRepo.Verify(
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
