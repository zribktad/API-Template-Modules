using Moq;
using Reviews.Domain;
using Reviews.Features.ProductSoftDelete;
using SharedKernel.Contracts.Events;
using Shouldly;
using Wolverine;
using Xunit;

namespace APITemplate.Tests.Unit.Reviews;

[Trait("Category", "Unit")]
public sealed class ProductsBatchSoftDeletedHandlerTests
{
    private readonly Mock<IProductReviewRepository> _repositoryMock = new();

    [Fact]
    public async Task Handle_WhenReviewsAffected_ReturnsCacheInvalidation()
    {
        List<Guid> productIds = [Guid.NewGuid(), Guid.NewGuid()];
        ProductsBatchSoftDeletedNotification notification = new(
            productIds,
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow,
            Guid.NewGuid()
        );

        _repositoryMock
            .Setup(r =>
                r.BulkSoftDeleteByProductIdsAsync(
                    notification.ProductIds,
                    notification.TenantId,
                    notification.ActorId,
                    notification.DeletedAtUtc,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(5);

        OutgoingMessages result = await ProductsBatchSoftDeletedHandler.Handle(
            notification,
            _repositoryMock.Object,
            TestContext.Current.CancellationToken
        );

        _repositoryMock.Verify(
            r =>
                r.BulkSoftDeleteByProductIdsAsync(
                    notification.ProductIds,
                    notification.TenantId,
                    notification.ActorId,
                    notification.DeletedAtUtc,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );

        result.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Handle_WhenNoReviewsAffected_ReturnsEmpty()
    {
        ProductsBatchSoftDeletedNotification notification = new(
            [Guid.NewGuid()],
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow,
            Guid.NewGuid()
        );

        _repositoryMock
            .Setup(r =>
                r.BulkSoftDeleteByProductIdsAsync(
                    It.IsAny<IReadOnlyList<Guid>>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(0);

        OutgoingMessages result = await ProductsBatchSoftDeletedHandler.Handle(
            notification,
            _repositoryMock.Object,
            TestContext.Current.CancellationToken
        );

        result.ShouldBeEmpty();
    }
}
