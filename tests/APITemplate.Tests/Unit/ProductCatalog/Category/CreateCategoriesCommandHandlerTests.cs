using global::ProductCatalog;
using APITemplate.Tests.Unit.Infrastructure;
using BuildingBlocks.Application.Context;
using BuildingBlocks.Application.DTOs;
using BuildingBlocks.Domain.Interfaces;
using ErrorOr;
using global::ProductCatalog.Features.Category.CreateCategories;
using global::ProductCatalog.Interfaces;
using Moq;
using Shouldly;
using Wolverine;
using Xunit;
using CacheTags = global::ProductCatalog.Common.Events.CacheTags;

namespace APITemplate.Tests.Unit.ProductCatalog;

[Trait("Category", "Unit")]
public sealed class CreateCategoriesCommandHandlerTests
{
    private readonly Mock<ICategoryRepository> _repository = new();
    private readonly Mock<IUnitOfWork<ProductCatalogDbMarker>> _unitOfWork = new();

    public CreateCategoriesCommandHandlerTests()
    {
        UnitOfWorkTestHelper.SetupTransactionPassthrough(_unitOfWork);
    }

    [Fact]
    public async Task LoadAsync_ShouldCreateEntitiesFromRequestItems()
    {
        CreateCategoriesCommand command = new(
            new CreateCategoriesRequest([new CreateCategoryRequest("A", "Desc A"), new("B", null)])
        );

        (
            HandlerContinuation continuation,
            IReadOnlyList<global::ProductCatalog.Entities.Category>? entities,
            _
        ) = await CreateCategoriesCommandHandler.LoadAsync(
            command,
            TestContext.Current.CancellationToken
        );

        continuation.ShouldBe(HandlerContinuation.Continue);
        entities.ShouldNotBeNull();
        entities.Count.ShouldBe(2);
        entities[0].Name.ShouldBe("A");
        entities[1].Description.ShouldBeNull();
    }

    [Fact]
    public async Task HandleAsync_ShouldAddEntitiesAndEmitCategoryCacheInvalidation()
    {
        IReadOnlyList<global::ProductCatalog.Entities.Category> entities =
        [
            DomainTestDataFactory.Category(name: "A"),
            DomainTestDataFactory.Category(name: "B"),
        ];
        CreateCategoriesCommand command = new(
            new CreateCategoriesRequest([new("A", null), new("B", "D")])
        );

        (ErrorOr<BatchResponse> response, Wolverine.OutgoingMessages messages) =
            await CreateCategoriesCommandHandler.HandleAsync(
                command,
                entities,
                _repository.Object,
                _unitOfWork.Object,
                Mock.Of<ITenantProvider>(),
                TestContext.Current.CancellationToken
            );

        response.IsError.ShouldBeFalse();
        response.Value.SuccessCount.ShouldBe(2);
        _repository.Verify(
            r =>
                r.AddRangeAsync(
                    It.Is<IReadOnlyCollection<global::ProductCatalog.Entities.Category>>(x =>
                        x.Count == 2
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        messages.ShouldContainSingleCacheTag(CacheTags.Categories);
    }
}
