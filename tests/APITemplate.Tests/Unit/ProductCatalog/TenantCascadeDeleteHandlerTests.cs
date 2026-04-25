using Moq;
using ProductCatalog;
using ProductCatalog.Features.TenantCascadeDelete;
using ProductCatalog.Interfaces;
using SharedKernel.Contracts.Events;
using SharedKernel.Domain.Interfaces;
using SharedKernel.Domain.Options;
using Shouldly;
using Wolverine;
using Xunit;

namespace APITemplate.Tests.Unit.ProductCatalog;

[Trait("Category", "Unit")]
public sealed class TenantCascadeDeleteHandlerTests
{
    [Fact]
    public async Task Handle_UsesInjectedIdGeneratorForBatchNotificationCorrelationId()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid productId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Guid tenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        Guid actorId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        Guid correlationId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        DateTime deletedAtUtc = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        Mock<ICategoryRepository> categoryRepository = new();
        Mock<IProductRepository> productRepository = new();
        Mock<IProductDataLinkRepository> linkRepository = new();
        Mock<IUnitOfWork<ProductCatalogDbMarker>> unitOfWork = new();
        Mock<IIdGenerator> idGenerator = new();

        productRepository
            .Setup(r => r.GetNonDeletedIdsByTenantAsync(tenantId, ct))
            .ReturnsAsync([productId]);
        idGenerator.Setup(g => g.NewId()).Returns(correlationId);
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

        OutgoingMessages messages = await TenantCascadeDeleteHandler.Handle(
            new TenantSoftDeletedNotification(tenantId, actorId, deletedAtUtc),
            categoryRepository.Object,
            productRepository.Object,
            linkRepository.Object,
            unitOfWork.Object,
            idGenerator.Object,
            ct
        );

        ProductsBatchSoftDeletedNotification notification = messages
            .OfType<ProductsBatchSoftDeletedNotification>()
            .Single();
        notification.CorrelationId.ShouldBe(correlationId);
    }
}
