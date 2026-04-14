using Moq;
using Reviews.Domain;
using Reviews.Features.ProductSoftDelete;
using SharedKernel.Application.Events;
using SharedKernel.Contracts.Events;
using SharedKernel.Domain.Interfaces;
using SharedKernel.Domain.Options;
using Shouldly;
using Wolverine;
using Xunit;

namespace APITemplate.Tests.Unit.Reviews;

public sealed class ProductsBatchSoftDeletedHandlerTests
{
    private readonly Mock<IProductReviewRepository> _repositoryMock = new();
    private readonly Mock<IUnitOfWork<global::Reviews.ReviewsDbMarker>> _unitOfWorkMock = new();

    public ProductsBatchSoftDeletedHandlerTests()
    {
        _unitOfWorkMock
            .Setup(u =>
                u.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TransactionOptions?>()
                )
            )
            .Returns((Func<Task> action, CancellationToken _, TransactionOptions? _) => action());
    }

    [Fact]
    public async Task Handle_WhenReviewsExist_DeletesAndReturnsCacheInvalidation()
    {
        List<Guid> productIds = [Guid.NewGuid(), Guid.NewGuid()];
        ProductsBatchSoftDeletedNotification notification = new(
            productIds,
            Guid.NewGuid(),
            DateTime.UtcNow,
            Guid.NewGuid()
        );

        List<ProductReview> reviews =
        [
            ProductReview.Create(productIds[0], Guid.NewGuid(), Rating.FromPersistence(3), "r1"),
            ProductReview.Create(productIds[1], Guid.NewGuid(), Rating.FromPersistence(5), "r2"),
        ];

        _repositoryMock
            .Setup(r =>
                r.ListAsync(
                    It.IsAny<ProductReviewsForBatchSoftDeleteSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(reviews);

        OutgoingMessages result = await ProductsBatchSoftDeletedHandler.Handle(
            notification,
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            TestContext.Current.CancellationToken
        );

        _repositoryMock.Verify(
            r => r.DeleteRangeAsync(reviews, It.IsAny<CancellationToken>()),
            Times.Once
        );

        result.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Handle_WhenNoReviews_ReturnsEmpty()
    {
        ProductsBatchSoftDeletedNotification notification = new(
            [Guid.NewGuid()],
            Guid.NewGuid(),
            DateTime.UtcNow,
            Guid.NewGuid()
        );

        _repositoryMock
            .Setup(r =>
                r.ListAsync(
                    It.IsAny<ProductReviewsForBatchSoftDeleteSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new List<ProductReview>());

        OutgoingMessages result = await ProductsBatchSoftDeletedHandler.Handle(
            notification,
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            TestContext.Current.CancellationToken
        );

        _repositoryMock.Verify(
            r =>
                r.DeleteRangeAsync(
                    It.IsAny<IEnumerable<ProductReview>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );

        result.ShouldBeEmpty();
    }
}
