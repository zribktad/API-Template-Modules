using ErrorOr;
using Moq;
using ProductCatalog.Domain.Services;
using ProductCatalog.Features.Product.CreateProducts;
using ProductCatalog.ValueObjects;
using SharedKernel.Application.Batch;
using SharedKernel.Application.DTOs;
using Shouldly;
using Xunit;
using ProductEntity = ProductCatalog.Entities.Product;

namespace APITemplate.Tests.Unit.ProductCatalog;

public sealed class ProductBatchFactoryTests
{
    private readonly Mock<IProductBatchValidator<CreateProductRequest>> _validator = new();
    private readonly ProductBatchFactory _sut;

    public ProductBatchFactoryTests()
    {
        _sut = new ProductBatchFactory(_validator.Object);
    }

    [Fact]
    public async Task CreateAsync_WhenValidatorSucceeds_ReturnsConstructedEntities()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        List<CreateProductRequest> items = [new("Widget", "desc", 9.99m), new("Gadget", null, 19m)];
        Price[] prices = [Price.Create(9.99m).Value, Price.Create(19m).Value];
        _validator
            .Setup(v => v.ValidateAsync(items, ct, It.IsAny<IBatchRule<CreateProductRequest>[]>()))
            .ReturnsAsync(prices);

        ErrorOr<IReadOnlyList<ProductEntity>> result = await _sut.CreateAsync(items, ct);

        result.IsError.ShouldBeFalse();
        result.Value.Count.ShouldBe(2);
        result.Value[0].Name.ShouldBe("Widget");
        result.Value[1].Price.Value.ShouldBe(19m);
    }

    [Fact]
    public async Task CreateAsync_WhenValidatorFails_ForwardsErrorWithoutBuildingEntities()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        List<CreateProductRequest> items = [new("Bad", null, -1m)];
        BatchResponse failure = new(
            [new BatchResultItem(0, null, ["Price must be non-negative."])],
            0,
            1
        );
        _validator
            .Setup(v => v.ValidateAsync(items, ct, It.IsAny<IBatchRule<CreateProductRequest>[]>()))
            .ReturnsAsync(BatchResponseError.From(failure));

        ErrorOr<IReadOnlyList<ProductEntity>> result = await _sut.CreateAsync(items, ct);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe(BatchResponseError.Code);
        BatchResponseError.Unwrap(result.FirstError).ShouldBe(failure);
    }
}
