using ErrorOr;
using Moq;
using Reviews.Domain;
using Reviews.Features;
using SharedKernel.Contracts.Queries.Reviews;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Reviews.Handlers;

[Trait("Category", "Unit")]
public sealed class ProductReviewEdgeCaseTests
{
    [Fact]
    public async Task GetProductReviewsByProductIds_WhenRequestIsEmpty_ShouldReturnEmptyDictionaryWithoutRepositoryCall()
    {
        Mock<IProductReviewRepository> repository = new();

        ErrorOr<IReadOnlyDictionary<Guid, ProductReviewResponse[]>> result =
            await GetProductReviewsByProductIdsQueryHandler.HandleAsync(
                new GetProductReviewsByProductIdsQuery([]),
                repository.Object,
                TestContext.Current.CancellationToken
            );

        result.IsError.ShouldBeFalse();
        result.Value.ShouldBeEmpty();
        repository.Verify(
            r =>
                r.ListAsync(
                    It.IsAny<ProductReviewByProductIdsSpecification>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }
}
