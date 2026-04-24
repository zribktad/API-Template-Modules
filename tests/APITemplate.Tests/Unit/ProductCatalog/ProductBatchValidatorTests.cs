using ErrorOr;
using Moq;
using ProductCatalog.Domain.Services;
using ProductCatalog.Features.Product.CreateProducts;
using ProductCatalog.ValueObjects;
using SharedKernel.Application.Batch;
using SharedKernel.Application.DTOs;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.ProductCatalog;

[Trait("Category", "Unit")]
public sealed class ProductBatchValidatorTests
{
    private readonly Mock<IProductReferenceValidator> _referenceValidator = new();
    private readonly Mock<IBatchRule<CreateProductRequest>> _itemRule = new();
    private readonly ProductBatchValidator<CreateProductRequest> _sut;

    public ProductBatchValidatorTests()
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

        _sut = new ProductBatchValidator<CreateProductRequest>(
            _referenceValidator.Object,
            _itemRule.Object
        );
    }

    [Fact]
    public async Task ValidateAsync_WhenAllValid_ReturnsValidatedPrices()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        List<CreateProductRequest> items = [new("Widget", "desc", 9.99m), new("Gadget", null, 19m)];

        ErrorOr<IReadOnlyList<Price>> result = await _sut.ValidateAsync(items, ct);

        result.IsError.ShouldBeFalse();
        result.Value.Count.ShouldBe(2);
        result.Value[0].Value.ShouldBe(9.99m);
        result.Value[1].Value.ShouldBe(19m);
    }

    [Fact]
    public async Task ValidateAsync_WhenFluentRuleFails_ReturnsBatchResponseError()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        List<CreateProductRequest> items = [new("", null, 10m)];
        _itemRule
            .Setup(r => r.ApplyAsync(It.IsAny<BatchFailureContext<CreateProductRequest>>(), ct))
            .Callback<BatchFailureContext<CreateProductRequest>, CancellationToken>(
                (ctx, _) => ctx.AddFailure(0, null, "Name is required.")
            )
            .Returns(Task.CompletedTask);

        ErrorOr<IReadOnlyList<Price>> result = await _sut.ValidateAsync(items, ct);

        result.IsError.ShouldBeTrue();
        BatchResponse failure = BatchResponseError.Unwrap(result.FirstError);
        failure.FailureCount.ShouldBe(1);
        failure.SuccessCount.ShouldBe(0);
        failure.Failures[0].Errors.ShouldContain(e => e.Contains("Name"));
    }

    [Fact]
    public async Task ValidateAsync_WhenReferenceCheckFails_ReturnsBatchResponseError()
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

        ErrorOr<IReadOnlyList<Price>> result = await _sut.ValidateAsync(items, ct);

        result.IsError.ShouldBeTrue();
        BatchResponse failure = BatchResponseError.Unwrap(result.FirstError);
        failure.Failures[0].Errors.ShouldContain("Category not found");
    }

    [Fact]
    public async Task ValidateAsync_WhenPriceInvalid_ReturnsBatchResponseError()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        List<CreateProductRequest> items = [new("Valid Name", null, -1m)];

        ErrorOr<IReadOnlyList<Price>> result = await _sut.ValidateAsync(items, ct);

        result.IsError.ShouldBeTrue();
        BatchResponseError.Unwrap(result.FirstError).FailureCount.ShouldBe(1);
    }

    [Fact]
    public async Task ValidateAsync_SkipsReferenceCheckAndPriceForAlreadyFailedItems()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        List<CreateProductRequest> items = [new("", null, -10m), new("Good", null, 10m)];
        _itemRule
            .Setup(r => r.ApplyAsync(It.IsAny<BatchFailureContext<CreateProductRequest>>(), ct))
            .Callback<BatchFailureContext<CreateProductRequest>, CancellationToken>(
                (ctx, _) => ctx.AddFailure(0, null, "Name is required.")
            )
            .Returns(Task.CompletedTask);

        ErrorOr<IReadOnlyList<Price>> result = await _sut.ValidateAsync(items, ct);

        result.IsError.ShouldBeTrue();
        BatchResponse failure = BatchResponseError.Unwrap(result.FirstError);
        failure.Failures.ShouldHaveSingleItem();
        failure.Failures[0].Index.ShouldBe(0);
        failure.Failures[0].Errors.ShouldContain(e => e.Contains("Name"));
    }

    [Fact]
    public async Task ValidateAsync_MultipleFailureTypes_AllReported()
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

        ErrorOr<IReadOnlyList<Price>> result = await _sut.ValidateAsync(items, ct);

        result.IsError.ShouldBeTrue();
        BatchResponse failure = BatchResponseError.Unwrap(result.FirstError);
        failure.FailureCount.ShouldBe(2);
        failure.Failures.ShouldContain(f => f.Index == 0);
        failure.Failures.ShouldContain(f => f.Index == 1);
    }

    [Fact]
    public async Task ValidateAsync_AppliesAdditionalRulesAfterItemRule()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        List<CreateProductRequest> items = [new("Good", null, 10m)];
        Mock<IBatchRule<CreateProductRequest>> additionalRule = new();
        additionalRule
            .Setup(r => r.ApplyAsync(It.IsAny<BatchFailureContext<CreateProductRequest>>(), ct))
            .Callback<BatchFailureContext<CreateProductRequest>, CancellationToken>(
                (ctx, _) => ctx.AddFailure(0, null, "Custom rule failed.")
            )
            .Returns(Task.CompletedTask);

        ErrorOr<IReadOnlyList<Price>> result = await _sut.ValidateAsync(
            items,
            ct,
            additionalRule.Object
        );

        result.IsError.ShouldBeTrue();
        BatchResponseError
            .Unwrap(result.FirstError)
            .Failures[0]
            .Errors.ShouldContain(e => e.Contains("Custom"));
    }
}
