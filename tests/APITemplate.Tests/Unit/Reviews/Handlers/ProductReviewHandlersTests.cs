using APITemplate.Tests.Unit.Infrastructure;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Moq;
using Reviews;
using Reviews.Domain;
using Reviews.Features;
using SharedKernel.Application.Context;
using SharedKernel.Contracts.Queries.ProductCatalog;
using SharedKernel.Contracts.Queries.Reviews;
using SharedKernel.Domain.Common;
using SharedKernel.Domain.Interfaces;
using Shouldly;
using Wolverine;
using Xunit;
using CacheTags = SharedKernel.Contracts.Events.CacheTags;

namespace APITemplate.Tests.Unit.Reviews.Handlers;

[Trait("Category", "Unit")]
public sealed class ProductReviewHandlersTests
{
    private readonly Mock<IProductReviewRepository> _repository = new();
    private readonly Mock<IActorProvider> _actorProvider = new();
    private readonly Mock<IMessageBus> _bus = new();
    private readonly Mock<IUnitOfWork<ReviewsDbMarker>> _unitOfWork = new();

    public ProductReviewHandlersTests()
    {
        UnitOfWorkTestHelper.SetupTransactionPassthrough(_unitOfWork);
        UnitOfWorkTestHelper.SetupTransactionPassthrough<ReviewsDbMarker, ProductReview>(
            _unitOfWork
        );
    }

    [Fact]
    public async Task CreateLoad_WhenRatingInvalid_ShouldStop()
    {
        CreateProductReviewRequest request = new() { ProductId = Guid.NewGuid(), Rating = 9 };

        (
            HandlerContinuation continuation,
            CreateProductReviewCommandHandler.CreateProductReviewState? state,
            _
        ) = await CreateProductReviewCommandHandler.LoadAsync(
            new CreateProductReviewCommand(request),
            _bus.Object,
            _actorProvider.Object,
            TestContext.Current.CancellationToken
        );

        continuation.ShouldBe(HandlerContinuation.Stop);
        state.ShouldBeNull();
    }

    [Fact]
    public async Task CreateHandle_ShouldPersistReviewAndEmitCacheCascade()
    {
        Guid userId = Guid.NewGuid();
        Guid productId = Guid.NewGuid();
        _actorProvider.SetupGet(x => x.ActorId).Returns(userId);
        _bus.Setup(b =>
                b.InvokeAsync<ErrorOr<Success>>(
                    It.Is<ValidateProductExistsQuery>(q => q.ProductId == productId),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()
                )
            )
            .ReturnsAsync(Result.Success);
        _repository
            .Setup(r => r.AddAsync(It.IsAny<ProductReview>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (ProductReview review, CancellationToken _) =>
                {
                    review.Audit = new SharedKernel.Domain.Entities.AuditInfo
                    {
                        CreatedAtUtc = DateTime.UtcNow,
                    };
                    return review;
                }
            );

        CreateProductReviewRequest request = new()
        {
            ProductId = productId,
            Rating = 5,
            Comment = "Great",
        };
        (_, CreateProductReviewCommandHandler.CreateProductReviewState? state, _) =
            await CreateProductReviewCommandHandler.LoadAsync(
                new CreateProductReviewCommand(request),
                _bus.Object,
                _actorProvider.Object,
                TestContext.Current.CancellationToken
            );

        state.ShouldNotBeNull();
        (ErrorOr<ProductReviewResponse> result, OutgoingMessages messages) =
            await CreateProductReviewCommandHandler.HandleAsync(
                new CreateProductReviewCommand(request),
                state!,
                _repository.Object,
                _unitOfWork.Object,
                TestContext.Current.CancellationToken
            );

        result.IsError.ShouldBeFalse();
        messages.ShouldContainCacheTags([CacheTags.Reviews, CacheTags.Categories]);
    }

    [Fact]
    public async Task DeleteLoad_WhenReviewOwnedByDifferentUser_ShouldStop()
    {
        Guid reviewId = Guid.NewGuid();
        _actorProvider.SetupGet(x => x.ActorId).Returns(Guid.NewGuid());
        _repository
            .Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DomainTestDataFactory.ProductReview(userId: Guid.NewGuid()));

        (HandlerContinuation continuation, ProductReview? review, _) =
            await DeleteProductReviewCommandHandler.LoadAsync(
                new DeleteProductReviewCommand(reviewId),
                _repository.Object,
                _actorProvider.Object,
                TestContext.Current.CancellationToken
            );

        continuation.ShouldBe(HandlerContinuation.Stop);
        review.ShouldBeNull();
    }

    [Fact]
    public async Task QueryHandlers_ShouldReturnRepositoryResponses()
    {
        Guid id = Guid.NewGuid();
        ProductReview review = DomainTestDataFactory.ProductReview(id: id);
        _repository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);
        _repository
            .Setup(r =>
                r.ListAsync(
                    It.IsAny<ProductReviewByProductIdSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([review.ToResponse()]);
        _repository
            .Setup(r =>
                r.ListAsync(
                    It.IsAny<ProductReviewByProductIdsSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([review.ToResponse()]);
        _repository
            .Setup(r =>
                r.GetPagedAsync(
                    It.IsAny<ProductReviewSpecification>(),
                    1,
                    10,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new PagedResponse<ProductReviewResponse>([review.ToResponse()], 1, 1, 10)
            );

        ErrorOr<ProductReviewResponse> byId = await GetProductReviewByIdQueryHandler.HandleAsync(
            new GetProductReviewByIdQuery(id),
            _repository.Object,
            TestContext.Current.CancellationToken
        );
        ErrorOr<IReadOnlyList<ProductReviewResponse>> byProduct =
            await GetProductReviewsByProductIdQueryHandler.HandleAsync(
                new GetProductReviewsByProductIdQuery(review.ProductId),
                _repository.Object,
                TestContext.Current.CancellationToken
            );
        ErrorOr<PagedResponse<ProductReviewResponse>> paged =
            await GetProductReviewsQueryHandler.HandleAsync(
                new GetProductReviewsQuery(new ProductReviewFilter(PageNumber: 1, PageSize: 10)),
                _repository.Object,
                TestContext.Current.CancellationToken
            );
        ErrorOr<IReadOnlyDictionary<Guid, ProductReviewResponse[]>> batched =
            await GetProductReviewsByProductIdsQueryHandler.HandleAsync(
                new GetProductReviewsByProductIdsQuery([review.ProductId]),
                _repository.Object,
                TestContext.Current.CancellationToken
            );

        byId.IsError.ShouldBeFalse();
        byProduct.IsError.ShouldBeFalse();
        paged.IsError.ShouldBeFalse();
        batched.IsError.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateHandle_WhenRepositoryFails_ShouldBubbleAndNotEmitCacheMessages()
    {
        Guid userId = Guid.NewGuid();
        Guid productId = Guid.NewGuid();
        _actorProvider.SetupGet(x => x.ActorId).Returns(userId);
        _bus.Setup(b =>
                b.InvokeAsync<ErrorOr<Success>>(
                    It.Is<ValidateProductExistsQuery>(q => q.ProductId == productId),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()
                )
            )
            .ReturnsAsync(Result.Success);
        _repository
            .Setup(r => r.AddAsync(It.IsAny<ProductReview>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("persist failed"));

        CreateProductReviewRequest request = new()
        {
            ProductId = productId,
            Rating = 4,
            Comment = "will fail",
        };
        (_, CreateProductReviewCommandHandler.CreateProductReviewState? state, _) =
            await CreateProductReviewCommandHandler.LoadAsync(
                new CreateProductReviewCommand(request),
                _bus.Object,
                _actorProvider.Object,
                TestContext.Current.CancellationToken
            );
        state.ShouldNotBeNull();

        Func<Task> act = async () =>
        {
            await CreateProductReviewCommandHandler.HandleAsync(
                new CreateProductReviewCommand(request),
                state!,
                _repository.Object,
                _unitOfWork.Object,
                TestContext.Current.CancellationToken
            );
        };

        await act.ShouldThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateHandle_WhenDbUpdateExceptionOccurs_ShouldBubble()
    {
        Guid userId = Guid.NewGuid();
        Guid productId = Guid.NewGuid();
        _actorProvider.SetupGet(x => x.ActorId).Returns(userId);
        _bus.Setup(b =>
                b.InvokeAsync<ErrorOr<Success>>(
                    It.Is<ValidateProductExistsQuery>(q => q.ProductId == productId),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()
                )
            )
            .ReturnsAsync(Result.Success);
        _repository
            .Setup(r => r.AddAsync(It.IsAny<ProductReview>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("fk", new Exception("inner")));

        CreateProductReviewRequest request = new()
        {
            ProductId = productId,
            Rating = 5,
            Comment = "review",
        };
        (_, CreateProductReviewCommandHandler.CreateProductReviewState? state, _) =
            await CreateProductReviewCommandHandler.LoadAsync(
                new CreateProductReviewCommand(request),
                _bus.Object,
                _actorProvider.Object,
                TestContext.Current.CancellationToken
            );
        state.ShouldNotBeNull();

        Func<Task> act = async () =>
        {
            await CreateProductReviewCommandHandler.HandleAsync(
                new CreateProductReviewCommand(request),
                state!,
                _repository.Object,
                _unitOfWork.Object,
                TestContext.Current.CancellationToken
            );
        };

        await act.ShouldThrowAsync<DbUpdateException>();
    }
}
