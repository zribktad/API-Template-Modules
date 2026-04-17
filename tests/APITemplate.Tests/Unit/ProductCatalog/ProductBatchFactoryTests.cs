using Moq;
using ProductCatalog.Domain.Services;
using ProductCatalog.Features.Product.CreateProducts;
using SharedKernel.Application.Batch;
using SharedKernel.Application.DTOs;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.ProductCatalog;

public sealed class ProductBatchFactoryTests
{
    private readonly Mock<IProductReferenceValidator> _referenceValidator = new();
    private readonly Mock<IBatchRule<CreateProductRequest>> _itemRule = new();
    private readonly ProductBatchFactory _sut;

    public ProductBatchFactoryTests()
    {
        _referenceValidator
            .Setup(v =>
                v.CheckReferencesAsync(
                    It.IsAny<IReadOnlyList<CreateProductRequest>>(),
                    It.IsAny<IReadOnlySet<int>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new List<BatchResultItem>());

        _itemRule
            .Setup(r =>
                r.ApplyAsync(
                    It.IsAny<BatchFailureContext<CreateProductRequest>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);

        _sut = new ProductBatchFactory(_referenceValidator.Object, _itemRule.Object);
    }

    [Fact]
    public async Task CreateAsync_WhenAllValid_ReturnsEntitiesAndNoFailure()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        List<CreateProductRequest> items = [new("Widget", "desc", 9.99m), new("Gadget", null, 19m)];

        ProductBatchCreateResult result = await _sut.CreateAsync(items, ct);

        result.Failure.ShouldBeNull();
        result.Entities.ShouldNotBeNull();
        result.Entities!.Count.ShouldBe(2);
        result.Entities[0].Name.ShouldBe("Widget");
        result.Entities[1].Price.Value.ShouldBe(19m);
    }

    [Fact]
    public async Task CreateAsync_WhenFluentRuleFails_ReturnsFailureAndNoEntities()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        List<CreateProductRequest> items = [new("", null, 10m)];
        _itemRule
            .Setup(r => r.ApplyAsync(It.IsAny<BatchFailureContext<CreateProductRequest>>(), ct))
            .Callback<BatchFailureContext<CreateProductRequest>, CancellationToken>(
                (ctx, _) => ctx.AddFailure(0, null, "Name is required.")
            )
            .Returns(Task.CompletedTask);

        ProductBatchCreateResult result = await _sut.CreateAsync(items, ct);

        result.Entities.ShouldBeNull();
        result.Failure.ShouldNotBeNull();
        result.Failure!.FailureCount.ShouldBe(1);
        result.Failure.SuccessCount.ShouldBe(0);
        result.Failure.Failures[0].Errors.ShouldContain(e => e.Contains("Name"));
    }

    [Fact]
    public async Task CreateAsync_WhenReferenceCheckFails_ReturnsFailureAndSkipsEntityCreation()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid missingCategoryId = Guid.NewGuid();
        List<CreateProductRequest> items = [new("Good", null, 10m, missingCategoryId)];
        _referenceValidator
            .Setup(v =>
                v.CheckReferencesAsync(
                    It.IsAny<IReadOnlyList<CreateProductRequest>>(),
                    It.IsAny<IReadOnlySet<int>>(),
                    ct
                )
            )
            .ReturnsAsync([new BatchResultItem(0, null, ["Category not found"])]);

        ProductBatchCreateResult result = await _sut.CreateAsync(items, ct);

        result.Entities.ShouldBeNull();
        result.Failure.ShouldNotBeNull();
        result.Failure!.Failures[0].Errors.ShouldContain("Category not found");
    }

    [Fact]
    public async Task CreateAsync_WhenPriceInvalid_ReturnsFailure()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        List<CreateProductRequest> items = [new("Valid Name", null, -1m)];

        ProductBatchCreateResult result = await _sut.CreateAsync(items, ct);

        result.Entities.ShouldBeNull();
        result.Failure.ShouldNotBeNull();
        result.Failure!.FailureCount.ShouldBe(1);
    }

    [Fact]
    public async Task CreateAsync_SkipsReferenceCheckAndPriceForAlreadyFailedItems()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        List<CreateProductRequest> items = [new("", null, -10m), new("Good", null, 10m)];
        _itemRule
            .Setup(r => r.ApplyAsync(It.IsAny<BatchFailureContext<CreateProductRequest>>(), ct))
            .Callback<BatchFailureContext<CreateProductRequest>, CancellationToken>(
                (ctx, _) => ctx.AddFailure(0, null, "Name is required.")
            )
            .Returns(Task.CompletedTask);

        ProductBatchCreateResult result = await _sut.CreateAsync(items, ct);

        result.Failure.ShouldNotBeNull();
        // Only the name failure: invalid price on item 0 was skipped because it already failed.
        result.Failure!.Failures.ShouldHaveSingleItem();
        result.Failure.Failures[0].Index.ShouldBe(0);
        result.Failure.Failures[0].Errors.ShouldContain(e => e.Contains("Name"));
    }

    [Fact]
    public async Task CreateAsync_MultipleFailureTypes_AllReported()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid missingCategoryId = Guid.NewGuid();
        List<CreateProductRequest> items =
        [
            new("", null, 10m),
            new("Good", null, 10m, missingCategoryId),
            new("Also Good", null, 20m),
        ];

        _itemRule
            .Setup(r => r.ApplyAsync(It.IsAny<BatchFailureContext<CreateProductRequest>>(), ct))
            .Callback<BatchFailureContext<CreateProductRequest>, CancellationToken>(
                (ctx, _) => ctx.AddFailure(0, null, "Name is required.")
            )
            .Returns(Task.CompletedTask);
        _referenceValidator
            .Setup(v =>
                v.CheckReferencesAsync(
                    It.IsAny<IReadOnlyList<CreateProductRequest>>(),
                    It.IsAny<IReadOnlySet<int>>(),
                    ct
                )
            )
            .ReturnsAsync([new BatchResultItem(1, null, ["Category not found"])]);

        ProductBatchCreateResult result = await _sut.CreateAsync(items, ct);

        result.Failure.ShouldNotBeNull();
        result.Failure!.FailureCount.ShouldBe(2);
        result.Failure.Failures.ShouldContain(f => f.Index == 0);
        result.Failure.Failures.ShouldContain(f => f.Index == 1);
    }
}
