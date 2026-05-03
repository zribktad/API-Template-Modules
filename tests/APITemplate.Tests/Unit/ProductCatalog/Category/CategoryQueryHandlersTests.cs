using BuildingBlocks.Domain.Common;
using ErrorOr;
using Moq;
using ProductCatalog.Entities;
using ProductCatalog.Features.Category.GetCategories;
using ProductCatalog.Features.Category.GetCategoryById;
using ProductCatalog.Features.Category.GetCategoryStats;
using ProductCatalog.Features.Category.Shared;
using ProductCatalog.Interfaces;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.ProductCatalog;

[Trait("Category", "Unit")]
public sealed class CategoryQueryHandlersTests
{
    private readonly Mock<ICategoryRepository> _repository = new();

    [Fact]
    public async Task GetCategories_ShouldReturnPagedResponseFromRepository()
    {
        CategoryFilter filter = new(Query: "cat", PageNumber: 2, PageSize: 5);
        PagedResponse<CategoryResponse> expected = new(
            [new CategoryResponse(Guid.NewGuid(), "A", null, DateTime.UtcNow)],
            10,
            2,
            5
        );
        _repository
            .Setup(r =>
                r.GetPagedAsync(
                    It.IsAny<CategorySpecification>(),
                    filter.PageNumber,
                    filter.PageSize,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(expected);

        ErrorOr<PagedResponse<CategoryResponse>> result =
            await GetCategoriesQueryHandler.HandleAsync(
                new GetCategoriesQuery(filter),
                _repository.Object,
                TestContext.Current.CancellationToken
            );

        result.IsError.ShouldBeFalse();
        result.Value.TotalCount.ShouldBe(10);
    }

    [Fact]
    public async Task GetCategoryById_WhenNotFound_ShouldReturnNotFoundError()
    {
        Guid id = Guid.NewGuid();
        _repository
            .Setup(r =>
                r.FirstOrDefaultAsync(
                    It.IsAny<CategoryByIdSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((CategoryResponse?)null);

        ErrorOr<CategoryResponse> result = await GetCategoryByIdQueryHandler.HandleAsync(
            new GetCategoryByIdQuery(id),
            _repository.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeTrue();
    }

    [Fact]
    public async Task GetCategoryStats_WhenFound_ShouldMapEntityToResponse()
    {
        Guid categoryId = Guid.NewGuid();
        _repository
            .Setup(r => r.GetStatsByIdAsync(categoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ProductCategoryStats
                {
                    CategoryId = categoryId,
                    CategoryName = "Cat",
                    ProductCount = 3,
                    AveragePrice = 12.5m,
                    TotalReviews = 7,
                }
            );

        ErrorOr<ProductCategoryStatsResponse> result =
            await GetCategoryStatsQueryHandler.HandleAsync(
                new GetCategoryStatsQuery(categoryId),
                _repository.Object,
                TestContext.Current.CancellationToken
            );

        result.IsError.ShouldBeFalse();
        result.Value.ProductCount.ShouldBe(3);
        result.Value.TotalReviews.ShouldBe(7);
    }
}
