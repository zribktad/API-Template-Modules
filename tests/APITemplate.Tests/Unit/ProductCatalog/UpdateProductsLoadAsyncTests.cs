using Moq;
using ProductCatalog.Domain.Services;
using ProductCatalog.Entities;
using ProductCatalog.Features.Product.Shared;
using ProductCatalog.Features.Product.UpdateProducts;
using ProductCatalog.Interfaces;
using ProductCatalog.ValueObjects;
using SharedKernel.Application.Batch;
using SharedKernel.Application.Batch.Rules;
using SharedKernel.Application.DTOs;
using SharedKernel.Application.Validation;
using Shouldly;
using Wolverine;
using Xunit;

namespace APITemplate.Tests.Unit.ProductCatalog;

/// <summary>
///     Verifies the pre-load item-validation step in <see cref="UpdateProductsCommandHandler.LoadAsync" />:
///     rows failing item-level validation (e.g. <see cref="Guid.Empty" /> Id, enforced by the
///     <c>[NotEmpty]</c> data-annotation) must be excluded from the DB lookup.
/// </summary>
[Trait("Category", "Unit")]
public sealed class UpdateProductsLoadAsyncTests
{
    private readonly Mock<IProductRepository> _repository = new();
    private readonly Mock<IProductBatchValidator<UpdateProductItem>> _validator = new();
    private readonly IBatchRule<UpdateProductItem> _itemRule =
        new DataAnnotationsBatchRule<UpdateProductItem>(new DataAnnotationsValidator());

    [Fact]
    public async Task LoadAsync_WhenAllIdsAreGuidEmpty_DoesNotQueryRepository()
    {
        UpdateProductItem item = new(Guid.Empty, "Name", null, 10m);
        UpdateProductsRequest request = new([item]);

        _validator
            .Setup(v =>
                v.ValidateAsync(
                    It.IsAny<IReadOnlyList<UpdateProductItem>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<IBatchRule<UpdateProductItem>[]>()
                )
            )
            .ReturnsAsync(BatchResponseError.From(new BatchResponse([], 0, 1)));

        (HandlerContinuation continuation, _, _) = await UpdateProductsCommandHandler.LoadAsync(
            new UpdateProductsCommand(request),
            _repository.Object,
            _validator.Object,
            _itemRule,
            TestContext.Current.CancellationToken
        );

        continuation.ShouldBe(HandlerContinuation.Stop);
        _repository.Verify(
            r =>
                r.ListAsync(
                    It.IsAny<ProductsByIdsWithLinksSpecification>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task LoadAsync_WithMixOfValidAndEmptyIds_StillQueriesForValidRows()
    {
        // Pre-filter must not drop the whole batch when only some rows are malformed: the valid
        // row still needs its entity loaded for the subsequent update step.
        Guid validId = Guid.NewGuid();
        UpdateProductsRequest request = new([
            new UpdateProductItem(Guid.Empty, "Bad", null, 10m),
            new UpdateProductItem(validId, "Good", null, 10m),
        ]);

        _repository
            .Setup(r =>
                r.ListAsync(
                    It.IsAny<ProductsByIdsWithLinksSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([
                new Product
                {
                    Id = validId,
                    Name = "Good",
                    Price = Price.FromPersistence(10m),
                    Audit = new() { CreatedAtUtc = DateTime.UtcNow },
                    ProductDataLinks = [],
                },
            ]);

        _validator
            .Setup(v =>
                v.ValidateAsync(
                    It.IsAny<IReadOnlyList<UpdateProductItem>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<IBatchRule<UpdateProductItem>[]>()
                )
            )
            .ReturnsAsync(BatchResponseError.From(new BatchResponse([], 1, 1)));

        await UpdateProductsCommandHandler.LoadAsync(
            new UpdateProductsCommand(request),
            _repository.Object,
            _validator.Object,
            _itemRule,
            TestContext.Current.CancellationToken
        );

        _repository.Verify(
            r =>
                r.ListAsync(
                    It.IsAny<ProductsByIdsWithLinksSpecification>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }
}
