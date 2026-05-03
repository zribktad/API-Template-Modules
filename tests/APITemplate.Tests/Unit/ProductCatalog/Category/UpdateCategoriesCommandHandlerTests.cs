using global::ProductCatalog;
using APITemplate.Tests.Unit.Infrastructure;
using BuildingBlocks.Application.DTOs;
using BuildingBlocks.Domain.Interfaces;
using ErrorOr;
using global::ProductCatalog.Features.Category.Shared;
using global::ProductCatalog.Features.Category.UpdateCategories;
using global::ProductCatalog.Interfaces;
using Moq;
using Shouldly;
using Wolverine;
using Xunit;
using CacheTags = global::ProductCatalog.Common.Events.CacheTags;

namespace APITemplate.Tests.Unit.ProductCatalog;

[Trait("Category", "Unit")]
public sealed class UpdateCategoriesCommandHandlerTests
{
    private readonly Mock<ICategoryRepository> _repository = new();
    private readonly Mock<IUnitOfWork<ProductCatalogDbMarker>> _unitOfWork = new();

    public UpdateCategoriesCommandHandlerTests()
    {
        UnitOfWorkTestHelper.SetupTransactionPassthrough(_unitOfWork);
    }

    [Fact]
    public async Task LoadAsync_WhenAnyRequestedCategoryMissing_ShouldStopAndReturnFailureResponse()
    {
        Guid existingId = Guid.NewGuid();
        UpdateCategoriesCommand command = new(
            new UpdateCategoriesRequest([new(existingId, "A", null), new(Guid.NewGuid(), "B", "D")])
        );
        _repository
            .Setup(r =>
                r.ListAsync(It.IsAny<CategoriesByIdsSpecification>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([DomainTestDataFactory.Category(id: existingId)]);

        (
            HandlerContinuation continuation,
            UpdateCategoriesCommandHandler.UpdateCategoriesState? state,
            OutgoingMessages messages
        ) = await UpdateCategoriesCommandHandler.LoadAsync(
            command,
            _repository.Object,
            TestContext.Current.CancellationToken
        );

        continuation.ShouldBe(HandlerContinuation.Stop);
        state.ShouldBeNull();
        messages.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_ShouldUpdateEachEntityAndEmitCategoryCacheTag()
    {
        Guid idA = Guid.NewGuid();
        Guid idB = Guid.NewGuid();
        global::ProductCatalog.Entities.Category catA = DomainTestDataFactory.Category(
            id: idA,
            name: "Old A"
        );
        global::ProductCatalog.Entities.Category catB = DomainTestDataFactory.Category(
            id: idB,
            name: "Old B"
        );
        UpdateCategoriesCommand command = new(
            new UpdateCategoriesRequest([new(idA, "New A", "D1"), new(idB, "New B", null)])
        );
        UpdateCategoriesCommandHandler.UpdateCategoriesState state = new(
            command.Request.Items,
            new Dictionary<Guid, global::ProductCatalog.Entities.Category>
            {
                [idA] = catA,
                [idB] = catB,
            }
        );

        (ErrorOr<BatchResponse> result, OutgoingMessages messages) =
            await UpdateCategoriesCommandHandler.HandleAsync(
                command,
                state,
                _repository.Object,
                _unitOfWork.Object,
                TestContext.Current.CancellationToken
            );

        result.IsError.ShouldBeFalse();
        catA.Name.ShouldBe("New A");
        catB.Name.ShouldBe("New B");
        _repository.Verify(
            r =>
                r.UpdateAsync(
                    It.IsAny<global::ProductCatalog.Entities.Category>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(2)
        );
        messages.ShouldContainSingleCacheTag(CacheTags.Categories);
    }
}
