using APITemplate.Application.Common.Context;
using APITemplate.Application.Common.Events;
using APITemplate.Application.Features.Product.Repositories;
using APITemplate.Application.Features.ProductReview;
using APITemplate.Application.Features.ProductReview.Specifications;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using APITemplate.Domain.Options;
using ErrorOr;
using Moq;
using Shouldly;
using Wolverine;
using Xunit;

namespace APITemplate.Tests.Unit.Handlers;

public class ProductReviewRequestHandlersTests
{
    private readonly Mock<IProductReviewRepository> _reviewRepoMock;
    private readonly Mock<IProductRepository> _productRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IActorProvider> _actorProviderMock;
    private readonly Mock<IMessageBus> _busMock;
    private readonly Guid _currentUserId = Guid.NewGuid();

    public ProductReviewRequestHandlersTests()
    {
        _reviewRepoMock = new Mock<IProductReviewRepository>();
        _productRepoMock = new Mock<IProductRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _actorProviderMock = new Mock<IActorProvider>();
        _busMock = new Mock<IMessageBus>();
        _actorProviderMock.Setup(a => a.ActorId).Returns(_currentUserId);
        _unitOfWorkMock.SetupImmediateTransactionExecution();
        _unitOfWorkMock.SetupImmediateTransactionExecution<ProductReview>();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllReviews()
    {
        var ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        var items = new List<ProductReviewResponse>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), userId, null, 5, DateTime.UtcNow),
            new(Guid.NewGuid(), Guid.NewGuid(), userId, null, 3, DateTime.UtcNow),
        };

        var paged = new PagedResponse<ProductReviewResponse>(items, 2, 1, 10);
        _reviewRepoMock
            .Setup(r =>
                r.GetPagedAsync(
                    It.IsAny<ProductReviewSpecification>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(paged);

        var result = await GetProductReviewsQueryHandler.HandleAsync(
            new GetProductReviewsQuery(new ProductReviewFilter()),
            _reviewRepoMock.Object,
            ct
        );

        result.IsError.ShouldBeFalse();
        result.Value.Items.Count().ShouldBe(2);
        result.Value.TotalCount.ShouldBe(2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetByIdAsync_ReturnsExpectedResult(bool reviewExists)
    {
        var ct = TestContext.Current.CancellationToken;
        var reviewId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        ProductReview? entity = reviewExists
            ? new ProductReview
            {
                Id = reviewId,
                ProductId = Guid.NewGuid(),
                UserId = userId,
                Rating = 4,
                Audit = new() { CreatedAtUtc = DateTime.UtcNow },
            }
            : null;

        _reviewRepoMock
            .Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await GetProductReviewByIdQueryHandler.HandleAsync(
            new GetProductReviewByIdQuery(reviewId),
            _reviewRepoMock.Object,
            ct
        );

        if (reviewExists)
        {
            result.IsError.ShouldBeFalse();
            result.Value.UserId.ShouldBe(userId);
            result.Value.Rating.ShouldBe(4);
        }
        else
        {
            result.IsError.ShouldBeTrue();
            result.FirstError.Type.ShouldBe(ErrorType.NotFound);
        }
    }

    [Fact]
    public async Task GetByProductIdAsync_ReturnsReviewsForProduct()
    {
        var ct = TestContext.Current.CancellationToken;
        var productId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var items = new List<ProductReviewResponse>
        {
            new(Guid.NewGuid(), productId, userId, null, 5, DateTime.UtcNow),
        };

        _reviewRepoMock
            .Setup(r =>
                r.ListAsync(
                    It.IsAny<ProductReviewByProductIdSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(items);

        var result = await GetProductReviewsByProductIdQueryHandler.HandleAsync(
            new GetProductReviewsByProductIdQuery(productId),
            _reviewRepoMock.Object,
            ct
        );

        result.IsError.ShouldBeFalse();
        result.Value.Count.ShouldBe(1);
        result.Value[0].ProductId.ShouldBe(productId);
    }

    [Fact]
    public async Task CreateAsync_WhenProductExists_CreatesReview()
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Price = 10m,
            Audit = new() { CreatedAtUtc = DateTime.UtcNow },
        };
        var request = new CreateProductReviewRequest(product.Id, "Great!", 5);

        _productRepoMock
            .Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _reviewRepoMock
            .Setup(r => r.AddAsync(It.IsAny<ProductReview>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductReview rv, CancellationToken _) => rv);

        var result = await CreateProductReviewCommandHandler.HandleAsync(
            new CreateProductReviewCommand(request),
            _reviewRepoMock.Object,
            _productRepoMock.Object,
            _unitOfWorkMock.Object,
            _actorProviderMock.Object,
            _busMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        result.Value.UserId.ShouldBe(_currentUserId);
        result.Value.Rating.ShouldBe(5);
        result.Value.ProductId.ShouldBe(product.Id);
        result.Value.Id.ShouldNotBe(Guid.Empty);

        _reviewRepoMock.Verify(
            r => r.AddAsync(It.IsAny<ProductReview>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        _unitOfWorkMock.Verify(
            u =>
                u.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task<ProductReview>>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TransactionOptions?>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task CreateAsync_WhenProductNotFound_ReturnsNotFoundError()
    {
        _productRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var request = new CreateProductReviewRequest(Guid.NewGuid(), null, 3);

        var result = await CreateProductReviewCommandHandler.HandleAsync(
            new CreateProductReviewCommand(request),
            _reviewRepoMock.Object,
            _productRepoMock.Object,
            _unitOfWorkMock.Object,
            _actorProviderMock.Object,
            _busMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_WhenOwner_CallsRepositoryDelete()
    {
        var id = Guid.NewGuid();
        var review = new ProductReview
        {
            Id = id,
            UserId = _currentUserId,
            Rating = 3,
        };

        _reviewRepoMock
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);

        var result = await DeleteProductReviewCommandHandler.HandleAsync(
            new DeleteProductReviewCommand(id),
            _reviewRepoMock.Object,
            _unitOfWorkMock.Object,
            _actorProviderMock.Object,
            _busMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        _reviewRepoMock.Verify(
            r => r.DeleteAsync(It.IsAny<ProductReview>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        _unitOfWorkMock.Verify(
            u =>
                u.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TransactionOptions?>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task DeleteAsync_WhenNotOwner_ReturnsForbiddenError()
    {
        var id = Guid.NewGuid();
        var review = new ProductReview
        {
            Id = id,
            UserId = Guid.NewGuid(),
            Rating = 3,
        };

        _reviewRepoMock
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);

        var result = await DeleteProductReviewCommandHandler.HandleAsync(
            new DeleteProductReviewCommand(id),
            _reviewRepoMock.Object,
            _unitOfWorkMock.Object,
            _actorProviderMock.Object,
            _busMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Forbidden);
        _reviewRepoMock.Verify(
            r => r.DeleteAsync(It.IsAny<ProductReview>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task DeleteAsync_WhenNotFound_ReturnsNotFoundError()
    {
        var id = Guid.NewGuid();

        _reviewRepoMock
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductReview?)null);

        var result = await DeleteProductReviewCommandHandler.HandleAsync(
            new DeleteProductReviewCommand(id),
            _reviewRepoMock.Object,
            _unitOfWorkMock.Object,
            _actorProviderMock.Object,
            _busMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
    }
}
