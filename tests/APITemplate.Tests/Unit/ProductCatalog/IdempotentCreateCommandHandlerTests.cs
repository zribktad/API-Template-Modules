using ErrorOr;
using Moq;
using ProductCatalog;
using ProductCatalog.Features.Product.IdempotentCreate;
using ProductCatalog.Interfaces;
using SharedKernel.Domain.Interfaces;
using SharedKernel.Domain.Options;
using Shouldly;
using Xunit;
using ProductEntity = ProductCatalog.Entities.Product;

namespace APITemplate.Tests.Unit.ProductCatalog;

[Trait("Category", "Unit")]
public sealed class IdempotentCreateCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_UsesInjectedIdGeneratorForCreatedProductId()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid generatedId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        Mock<IProductRepository> repository = new();
        Mock<IUnitOfWork<ProductCatalogDbMarker>> unitOfWork = new();
        Mock<IIdGenerator> idGenerator = new();
        ProductEntity? persisted = null;

        idGenerator.Setup(g => g.NewId()).Returns(generatedId);
        repository
            .Setup(r => r.AddAsync(It.IsAny<ProductEntity>(), ct))
            .Callback<ProductEntity, CancellationToken>((entity, _) => persisted = entity)
            .ReturnsAsync((ProductEntity entity, CancellationToken _) => entity);
        unitOfWork
            .Setup(u =>
                u.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task>>(),
                    ct,
                    It.IsAny<TransactionOptions?>()
                )
            )
            .Returns<Func<Task>, CancellationToken, TransactionOptions?>(
                async (action, _, _) => await action()
            );

        ErrorOr<IdempotentCreateResponse> result = await IdempotentCreateCommandHandler.HandleAsync(
            new IdempotentCreateCommand(new IdempotentCreateRequest("Widget", "desc")),
            repository.Object,
            unitOfWork.Object,
            idGenerator.Object,
            ct
        );

        result.IsError.ShouldBeFalse();
        persisted.ShouldNotBeNull();
        persisted!.Id.ShouldBe(generatedId);
        result.Value.Id.ShouldBe(generatedId);
    }
}
