using global::ProductCatalog;
using APITemplate.Tests.Unit.Infrastructure;
using BuildingBlocks.Application.Context;
using BuildingBlocks.Application.DTOs;
using BuildingBlocks.Domain.Interfaces;
using ErrorOr;
using global::ProductCatalog.Features.Category.UpdateCategories;
using global::ProductCatalog.Interfaces;
using Moq;
using Shouldly;
using Wolverine;
using Xunit;

namespace APITemplate.Tests.Unit.ProductCatalog;

[Trait("Category", "Unit")]
public sealed class CategoryEdgeCaseTests
{
    [Fact]
    public async Task UpdateCategories_WithDuplicateIds_ShouldUpdateEachBatchRow()
    {
        Guid categoryId = Guid.NewGuid();
        global::ProductCatalog.Entities.Category category = DomainTestDataFactory.Category(
            id: categoryId,
            name: "Old"
        );
        Mock<ICategoryRepository> repository = new();
        Mock<IUnitOfWork<ProductCatalogDbMarker>> unitOfWork = new();
        UnitOfWorkTestHelper.SetupTransactionPassthrough(unitOfWork);
        UpdateCategoriesCommand command = new(
            new UpdateCategoriesRequest([
                new(categoryId, "Name One", null),
                new(categoryId, "Name Two", null),
            ])
        );
        UpdateCategoriesCommandHandler.UpdateCategoriesState state = new(
            command.Request.Items,
            new Dictionary<Guid, global::ProductCatalog.Entities.Category>
            {
                [categoryId] = category,
            }
        );

        (ErrorOr<BatchResponse> result, OutgoingMessages _) =
            await UpdateCategoriesCommandHandler.HandleAsync(
                command,
                state,
                repository.Object,
                unitOfWork.Object,
                Mock.Of<ITenantProvider>(),
                TestContext.Current.CancellationToken
            );

        result.IsError.ShouldBeFalse();
        repository.Verify(
            r =>
                r.UpdateAsync(
                    It.IsAny<global::ProductCatalog.Entities.Category>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(2)
        );
        category.Name.ShouldBe("Name Two");
    }

    [Fact]
    public async Task UpdateCategories_WhenTransactionFails_ShouldBubbleException()
    {
        Mock<ICategoryRepository> repository = new();
        Mock<IUnitOfWork<ProductCatalogDbMarker>> unitOfWork = new();
        UnitOfWorkTestHelper.SetupTransactionFailure(
            unitOfWork,
            new InvalidOperationException("tx failure")
        );
        Guid categoryId = Guid.NewGuid();
        UpdateCategoriesCommand command = new(
            new UpdateCategoriesRequest([new(categoryId, "New Name", null)])
        );
        UpdateCategoriesCommandHandler.UpdateCategoriesState state = new(
            command.Request.Items,
            new Dictionary<Guid, global::ProductCatalog.Entities.Category>
            {
                [categoryId] = DomainTestDataFactory.Category(id: categoryId),
            }
        );

        Func<Task> act = () =>
            UpdateCategoriesCommandHandler.HandleAsync(
                command,
                state,
                repository.Object,
                unitOfWork.Object,
                Mock.Of<ITenantProvider>(),
                TestContext.Current.CancellationToken
            );

        await act.ShouldThrowAsync<InvalidOperationException>();
    }
}
