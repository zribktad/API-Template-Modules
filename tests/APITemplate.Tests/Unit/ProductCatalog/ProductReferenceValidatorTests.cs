using Moq;
using ProductCatalog.Domain.Services;
using ProductCatalog.Entities;
using ProductCatalog.Entities.ProductData;
using ProductCatalog.Features.Category.Shared;
using ProductCatalog.Features.Product.CreateProducts;
using ProductCatalog.Interfaces;
using SharedKernel.Application.DTOs;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.ProductCatalog;

public sealed class ProductReferenceValidatorTests
{
    private readonly Mock<ICategoryRepository> _categoryRepository = new();
    private readonly Mock<IProductDataRepository> _productDataRepository = new();
    private readonly ProductReferenceValidator _sut;

    public ProductReferenceValidatorTests()
    {
        _sut = new ProductReferenceValidator(
            _categoryRepository.Object,
            _productDataRepository.Object
        );
    }

    [Fact]
    public async Task CheckReferencesAsync_WhenAllReferencesValid_ReturnsNoFailures()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid categoryId = Guid.NewGuid();
        Guid productDataId = Guid.NewGuid();
        List<CreateProductRequest> items = [new("Name", null, 10m, categoryId, [productDataId])];

        _categoryRepository
            .Setup(r =>
                r.ListAsync(It.IsAny<CategoriesByIdsSpecification>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([new Category { Id = categoryId, Name = "C" }]);
        _productDataRepository
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), ct))
            .ReturnsAsync([new ImageProductData { Id = productDataId, Title = "T" }]);

        List<BatchResultItem> failures = await _sut.CheckReferencesAsync(
            items,
            new HashSet<int>(),
            ct
        );

        failures.ShouldBeEmpty();
    }

    [Fact]
    public async Task CheckReferencesAsync_WhenCategoryMissing_ReturnsFailureForItem()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid missingCategoryId = Guid.NewGuid();
        List<CreateProductRequest> items = [new("Name", null, 10m, missingCategoryId)];

        _categoryRepository
            .Setup(r =>
                r.ListAsync(It.IsAny<CategoriesByIdsSpecification>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([]);

        List<BatchResultItem> failures = await _sut.CheckReferencesAsync(
            items,
            new HashSet<int>(),
            ct
        );

        failures.ShouldHaveSingleItem();
        failures[0].Index.ShouldBe(0);
        failures[0].Errors.ShouldContain(e => e.Contains("Category"));
    }

    [Fact]
    public async Task CheckReferencesAsync_WhenProductDataMissing_ReturnsFailureForItem()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid missingPd = Guid.NewGuid();
        List<CreateProductRequest> items = [new("Name", null, 10m, null, [missingPd])];

        _productDataRepository
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), ct))
            .ReturnsAsync([]);

        List<BatchResultItem> failures = await _sut.CheckReferencesAsync(
            items,
            new HashSet<int>(),
            ct
        );

        failures.ShouldHaveSingleItem();
        failures[0].Errors.ShouldContain(e => e.Contains("Product data"));
    }

    [Fact]
    public async Task CheckReferencesAsync_WhenCategoryAndProductDataMissing_MergesErrorsForSameIndex()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid categoryId = Guid.NewGuid();
        Guid pdId = Guid.NewGuid();
        List<CreateProductRequest> items = [new("Name", null, 10m, categoryId, [pdId])];

        _categoryRepository
            .Setup(r =>
                r.ListAsync(It.IsAny<CategoriesByIdsSpecification>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([]);
        _productDataRepository
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), ct))
            .ReturnsAsync([]);

        List<BatchResultItem> failures = await _sut.CheckReferencesAsync(
            items,
            new HashSet<int>(),
            ct
        );

        failures.ShouldHaveSingleItem();
        failures[0].Index.ShouldBe(0);
        failures[0].Errors.Count.ShouldBe(2);
    }

    [Fact]
    public async Task CheckReferencesAsync_SkipsIndicesInSkipSet()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid missingCategoryId = Guid.NewGuid();
        List<CreateProductRequest> items =
        [
            new("A", null, 10m, missingCategoryId),
            new("B", null, 10m, missingCategoryId),
        ];

        _categoryRepository
            .Setup(r =>
                r.ListAsync(It.IsAny<CategoriesByIdsSpecification>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([]);

        List<BatchResultItem> failures = await _sut.CheckReferencesAsync(
            items,
            new HashSet<int> { 0 },
            ct
        );

        failures.ShouldHaveSingleItem();
        failures[0].Index.ShouldBe(1);
    }

    [Fact]
    public async Task CheckReferencesAsync_WhenNoReferencesInItems_DoesNotQueryRepositories()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        List<CreateProductRequest> items = [new("Name", null, 10m)];

        List<BatchResultItem> failures = await _sut.CheckReferencesAsync(
            items,
            new HashSet<int>(),
            ct
        );

        failures.ShouldBeEmpty();
        _categoryRepository.Verify(
            r =>
                r.ListAsync(
                    It.IsAny<CategoriesByIdsSpecification>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        _productDataRepository.Verify(
            r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
